#!/usr/bin/env bash
# Packs each validated library against a derived baseline, so a public API
# removed or changed since the last shipped version fails the build.
#
# The baseline is derived every time it is needed and never declared beside the
# version it trails: it is the <VersionPrefix> the project declared at the last
# release tag. Until the first release under this scheme, no tag declares one,
# so the baseline falls back to the highest stable version on nuget.org. That
# fallback is a one-time bridge; once a release tag declares every library's
# version, it is never taken again.
#
# Past a break, the ways through are a major bump, which skips the comparison,
# or a CompatibilitySuppressions.xml committed beside the project
# (dotnet pack <project> -c Release -p:ApiCompatGenerateSuppressionFile=true, with
# the two environment variables below set), which a reviewer sees in the diff.
#
# The baseline reaches MSBuild through PACKAGE_VALIDATION_BASELINE_PROJECT and
# PACKAGE_VALIDATION_BASELINE_VERSION, which Directory.Build.props turns into
# PackageValidationBaselineVersion for that one project. Passing the baseline as
# an MSBuild global property instead would flow it into every project reference,
# where NuGet would look for the referenced package at a version it never
# published. The comment in Directory.Build.props carries the detail.
#
# Needs the release tags, which a shallow clone lacks, so it runs in CI rather
# than before a push.

set -euo pipefail

cd "$(dirname "$0")/../.."
source .github/scripts/versioning-lib.sh

tag=$(latest_release_tag)
out=$(mktemp -d)
trap 'rm -rf "$out"' EXIT

if [ -n "$tag" ]; then
    echo "Latest release tag reachable from HEAD: $tag."
else
    echo "No release tag matching ${RELEASE_TAG_PATTERN} is reachable from HEAD."
fi
echo

failed=0

while read -r project; do
    declared=$(declared_version "$project")
    id=$(package_id "$project")

    if [ -z "$declared" ]; then
        echo "FAIL $project declares no <VersionPrefix>."
        failed=1
        continue
    fi

    baseline=""
    source_of_baseline=""
    if [ -n "$tag" ]; then
        baseline=$(version_at_tag "$tag" "$project")
        [ -n "$baseline" ] && source_of_baseline="release tag $tag"
    fi
    if [ -z "$baseline" ]; then
        # An unreachable feed is not the same answer as a package that was never
        # published, and reporting it as one would put a false claim in the log.
        if ! baseline=$(nuget_latest_stable "$id"); then
            echo "FAIL $project: nuget.org could not be reached to derive a baseline for $id."
            echo "     That is an environment finding, not an API break and not a missing package."
            failed=1
            echo
            continue
        fi
        source_of_baseline="nuget.org"
    fi

    if [ -z "$baseline" ]; then
        echo "FAIL $project has no baseline: no version at $tag and no stable $id on nuget.org."
        failed=1
        continue
    fi

    echo "$project declares $declared; baseline $baseline from $source_of_baseline."

    declared_major=$(major_of "$declared")
    baseline_major=$(major_of "$baseline")

    if [ "$declared_major" -lt "$baseline_major" ]; then
        echo "FAIL $project declares major $declared_major below the baseline's $baseline_major."
        failed=1
        continue
    fi

    if [ "$declared_major" -gt "$baseline_major" ]; then
        echo "     Comparison skipped: $declared declares a breaking release above baseline $baseline."
        if ! dotnet pack "$project" -c Release -o "$out" --nologo; then
            echo "FAIL $project failed to pack."
            failed=1
        fi
        echo
        continue
    fi

    log=$out/$project.pack.log
    if PACKAGE_VALIDATION_BASELINE_PROJECT="$project" \
       PACKAGE_VALIDATION_BASELINE_VERSION="$baseline" \
       dotnet pack "$project" -c Release -o "$out" --nologo > "$log" 2>&1; then
        cat "$log"
        echo
        continue
    fi

    cat "$log"

    # Say which of three different things went wrong. A pack can fail without
    # any API having broken, and reporting every failure as a break would put a
    # claim in the log that the output does not support.
    if grep -qE 'error CP[0-9]{4}|API breaking changes found' "$log"; then
        echo "FAIL $project breaks its API against baseline $baseline."
        echo "     Restore the API, bump the major in $(project_file "$project"), or commit a"
        echo "     CompatibilitySuppressions.xml beside the project and say why in the description."
    elif grep -q "error NU1102.*$id" "$log"; then
        echo "FAIL $project has no baseline package to compare against: $id $baseline is not on the feed."
        echo "     That is not an API break. A release tag declared $baseline for this project but"
        echo "     the package was never published under it, so the baseline cannot be resolved."
    else
        echo "FAIL $project failed to pack, for a reason that is neither an API break nor a"
        echo "     missing baseline. The pack output above says what happened."
    fi
    failed=1
    echo
done < <(validated_projects)

if [ "$failed" -ne 0 ]; then
    echo "Package API validation failed."
    exit 1
fi
echo "Package API validation passed."
