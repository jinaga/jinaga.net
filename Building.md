# Building Jinaga.NET

To build Jinaga.NET locally, either use Visual Studio, or the command line.
Using the command line, execute the following commands from the root folder:

```bash
dotnet restore
dotnet build
dotnet test
```

## Versioning

This repository uses [MinVer](https://github.com/adamralph/minver) for versioning.
The version number is determined by the Git tag of the commit.
If the commit is not tagged, the version number is determined by the latest tag and the number of commits since that tag.

To explicitly control the version number, create a Git tag as described below.

## Releasing to NuGet

GitHub Actions build, test, and release the library to NuGet.
The workflow `nuget.yml` is triggered on the creation of a release.
Create a tag with the name matching the desired version number and push the tag.
For example:

```bash
git tag 0.2.25
git push --tags
```

Then on GitHub, create a release for that tag.
Auto-generate the description of the release so that all of the intervening pull requests are listed.

```bash
gh release create 0.2.25 --generate-notes
```
