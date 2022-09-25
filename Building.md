# Building Jinaga.NET

To build Jinaga.NET locally, either use Visual Studio, or the command line.
Using the command line, execute the following commands from the root folder:

```bash
dotnet restore
dotnet build
dotnet test
```

## Versioning

This repository uses Nerdbank.GitVersioning to automatically generate version numbers.
The version number is based on the Git tag, and the number of commits since the tag.
The version number is stored in the `version.json` file, and is used by the build process.
Please see the [Nerdbank.GitVersioning documentation](https://github.com/dotnet/Nerdbank.GitVersioning) for more information.

Install the Nerdbank.GitVersioning tool:

```bash
dotnet tool install -g nbgv
```

To increment the version number, use the following command:

```bash
nbgv prepare-release
```

## Releasing to NuGet

GitHub Actions build, test, and release the library to NuGet.
The workflow `nuget.yml` is triggered on the creation of a release.
Use `nbgv` to increment the version number and create a tag.

Then on GitHub, create a release for that tag.
Auto-generate the description of the release so that all of the intervening pull requests are listed.

```bash
gh release create v0.2.25 --generate-notes
```
