#!/usr/bin/env bash
# Refuses a change to a packable project that would ship under a version that
# project has already shipped.
#
# The version a package ships under is declared once, as <VersionPrefix> in its
# project file. Nothing derives it, so nothing stops a changed project from
# being published twice under one version. This check is where that invariant
# lives. It compares a project's declared version only with the version the same
# project declared at the last release, so there is still exactly one place a
# version is written.
#
# Needs the release tags, which a shallow clone lacks, so it runs in CI rather
# than before a push.

set -euo pipefail

cd "$(dirname "$0")/../.."
source .github/scripts/versioning-lib.sh

tag=$(latest_release_tag)

if [ -z "$tag" ]; then
    echo "No release tag matching ${RELEASE_TAG_PATTERN} is reachable from HEAD."
    echo "There is no shipped version to compare against. Nothing to check."
    exit 0
fi

echo "Comparing against release tag $tag."
echo

failed=0

while read -r project; do
    declared=$(declared_version "$project")
    at_tag=$(version_at_tag "$tag" "$project")
    changed=$(git diff --name-only "$tag..HEAD" -- "$project/")

    if [ -z "$declared" ]; then
        echo "FAIL $project declares no <VersionPrefix>."
        failed=1
        continue
    fi

    if [ -z "$changed" ]; then
        echo "ok   $project unchanged since $tag (declares $declared)"
        continue
    fi

    if [ -z "$at_tag" ]; then
        echo "ok   $project changed since $tag, which declared no version (declares $declared)"
        continue
    fi

    if [ "$declared" = "$at_tag" ]; then
        echo "FAIL $project changed since $tag but still declares $declared, the version it shipped at that tag."
        echo "     Bump the patch of <VersionPrefix> in $(project_file "$project"), and only that project's."
        failed=1
        continue
    fi

    echo "ok   $project changed since $tag, declares $declared, shipped $at_tag"
done < <(packable_projects)

echo
if [ "$failed" -ne 0 ]; then
    echo "Declared version check failed."
    exit 1
fi
echo "Declared version check passed."
