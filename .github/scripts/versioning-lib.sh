#!/usr/bin/env bash
# Shared helpers for the two versioning checks in build-test.yml.
#
# Nothing here hard-codes a project list. The packable set is read out of the
# project files that declare it, and the validated subset out of the property
# that turns validation on, so adding or removing a package changes one place.

# The release tag scheme release.yml publishes from.
RELEASE_TAG_PATTERN='^[0-9]{8}\.[0-9]+$'

# Every directory holding a project that declares a PackageId.
packable_projects() {
    local csproj dir
    for csproj in */*.csproj; do
        [ -f "$csproj" ] || continue
        if grep -q '<PackageId>' "$csproj"; then
            dir=${csproj%%/*}
            printf '%s\n' "$dir"
        fi
    done | sort
}

# The subset of those that turn package validation on.
validated_projects() {
    local dir
    while read -r dir; do
        if grep -q '<EnablePackageValidation>true</EnablePackageValidation>' "$(project_file "$dir")"; then
            printf '%s\n' "$dir"
        fi
    done < <(packable_projects)
}

# The project file for a project directory. Fails loudly rather than skipping,
# because a silent skip would read as a passing check.
project_file() {
    local dir=$1 csproj="$1/$1.csproj"
    if [ ! -f "$csproj" ]; then
        echo "versioning-lib: no project file at $csproj" >&2
        return 1
    fi
    printf '%s\n' "$csproj"
}

# The value of a single-line XML element, read from stdin. Empty when absent.
#
# sed quits at the first match rather than piping to `head -n 1`. With the pipe,
# head closing early can leave sed with a write error, and `pipefail` reports
# that as a failed pipeline even though the read succeeded.
read_element() {
    local name=$1
    sed -n "/<$name>/{s:.*<$name>\\([^<]*\\)</$name>.*:\\1:p;q;}"
}

# The VersionPrefix a project declares in the working tree.
declared_version() {
    read_element VersionPrefix < "$(project_file "$1")"
}

# The PackageId a project declares in the working tree.
package_id() {
    read_element PackageId < "$(project_file "$1")"
}

# The VersionPrefix a project declared at a tag. Empty when the project file is
# absent there, or declares none, which is the case for every tag cut while
# versions were computed from git height.
version_at_tag() {
    local tag=$1 dir=$2 csproj
    csproj=$(project_file "$dir") || return 1
    # `|| true` because the project file being absent at the tag is one of the
    # two documented empty cases, and `git show` reports it as a failure. The
    # callers assign this directly, so under `set -e` the failure would abort
    # the script instead of reaching their empty handling. That is the case for
    # a package added or renamed since the latest release tag.
    git show "$tag:$csproj" 2>/dev/null | read_element VersionPrefix || true
}

# The highest release tag reachable from HEAD, by the scheme's own ordering.
# Empty when none is reachable.
latest_release_tag() {
    # `|| true` because no tag matching the scheme is the documented empty, and
    # that is exactly what `grep` reports as a failure. Both scripts branch on
    # the empty result, and without this they would abort before reaching it.
    git tag --list --merged HEAD \
        | grep -E "$RELEASE_TAG_PATTERN" \
        | sort -t. -k1,1n -k2,2n \
        | tail -n 1 \
        || true
}

# The highest stable version of a package on nuget.org, which is the record of
# what has shipped. Prerelease versions are excluded.
# The highest stable version of a package on nuget.org, which is the record of
# what has shipped. Prerelease versions are excluded.
#
# Only a 404 is a legitimate empty: that package has never been published. Any
# other answer, and any transport failure, means the feed could not be reached
# or did not answer, which is a different fact and must not be reported as the
# same one. Those fail instead, and the caller branches on the two separately.
#
# The status code is read from the response rather than from curl's exit status,
# because curl reports every HTTP error alike: `-f` exits 22 for a 403 and a 500
# just as it does for a 404, so an exit status cannot tell "never published"
# apart from "the feed is refusing us".
nuget_latest_stable() {
    local id lowered url response code body
    id=$1
    lowered=$(printf '%s' "$id" | tr '[:upper:]' '[:lower:]')
    url="https://api.nuget.org/v3-flatcontainer/$lowered/index.json"

    if ! response=$(curl -sSL -w '\n%{http_code}' "$url"); then
        echo "versioning-lib: could not reach nuget.org for $id" >&2
        return 1
    fi
    code=${response##*$'\n'}
    body=${response%$'\n'*}

    if [ "$code" = "404" ]; then
        return 0
    fi
    if [ "$code" != "200" ]; then
        echo "versioning-lib: nuget.org answered HTTP $code for $id" >&2
        return 1
    fi

    printf '%s' "$body" | python3 -I -c '
import json, sys
versions = [v for v in json.load(sys.stdin)["versions"] if "-" not in v]
def key(v):
    return tuple(int(p) for p in v.split("."))
print(max(versions, key=key) if versions else "")
'
}

major_of() {
    printf '%s\n' "${1%%.*}"
}
