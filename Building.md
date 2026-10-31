# Building Jinaga.NET

To build Jinaga.NET locally, either use Visual Studio, or the command line.
Using the command line, execute the following commands from the root folder:

```bash
dotnet restore
dotnet build
dotnet test
```

## Versioning

Each packable project declares its own version as a `<VersionPrefix>` in its
`.csproj`: `Jinaga`, `Jinaga.UnitTest`, `Jinaga.Notebooks`,
`Jinaga.Store.SQLite`, `Jinaga.Maui` and `Jinaga.Tool`. That is the only place a
version is written. Nothing derives a version from git history, so a shallow
clone builds.

It is a `<VersionPrefix>` and not a `<Version>` so that a suffix can be appended
on the command line. `nuget.yml` passes `--version-suffix ci.<run number>` to
every `dotnet pack`, which makes everything it publishes to GitHub Packages a
prerelease such as `1.1.55-ci.812`, and so unable to take the plain version
`release.yml` later publishes to nuget.org. `release.yml` passes no suffix.

Two scripts under `.github/scripts/` guard the declared versions. Both read the
`YYYYMMDD.N` release tags, which a shallow clone lacks, so `build-test.yml` runs
them and nothing runs them before a push.

`check-declared-versions.sh` refuses a pull request that changes a project's
directory while leaving its `<VersionPrefix>` at the version that project
declared at the latest release tag. Bump the patch of the project the failure
names, and only that project's. A project whose directory has not changed since
that tag keeps its version.

`validate-package-apis.sh` packs each library against a baseline, so a public
API removed or changed since the last shipped version fails the build. The
baseline is derived each time, never declared: it is the `<VersionPrefix>` the
project declared at the latest release tag, or, until the first release under
this scheme, the highest stable version of the package on nuget.org. A pull
request it refuses does one of three things:

- restores the API;
- bumps the project's major version, which skips the comparison;
- commits a `CompatibilitySuppressions.xml` beside the project, generated with
  `dotnet pack <project> -c Release -p:ApiCompatGenerateSuppressionFile=true`
  (with `PACKAGE_VALIDATION_BASELINE_PROJECT` and
  `PACKAGE_VALIDATION_BASELINE_VERSION` set, as the script sets them), and says
  in its description why the break is intended.

A suppression file is deleted in the release that ships it, once the baseline
has moved past the break it records.

### Release Servicing Branches

When preparing a new major or minor release, create a release servicing branch
named `vx.y`. Set each project's `<VersionPrefix>` on that branch to the version
it will ship, and raise it on `main` to the next major or minor.

To produce a new service release, merge your fixes into the servicing branch,
bumping the patch of each project you changed.

## Releasing to NuGet

GitHub Actions build, test, and release the library to NuGet.
The workflow `nuget.yml` is triggered on the creation of a release.
Create a tag with the name containing the date and index and push the tag.
For example:

```bash
git tag 20241023.1
git push --tags
```

Then on GitHub, create a release for that tag.
Auto-generate the description of the release so that all of the intervening pull requests are listed.

```bash
gh release create 20241023.1 --generate-notes
```

Each package affected by that release will be published using its own version number.