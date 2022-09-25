# Building Jinaga.NET

To build Jinaga.NET locally, either use Visual Studio, or the command line.
Using the command line, execute the following commands from the root folder:

```bash
dotnet restore
dotnet build
dotnet test
```

## Releasing to NuGet

GitHub Actions build, test, and release the library to NuGet.
The workflow `nuget.yml` is triggered on the creation of a release.
Create a tag using the version number and push the tag.
For example:

```bash
git tag 0.2.25
git push --tags
```

Then on GitHub, create a release for that tag.
Auto-generate the description of the release so that all of the intervening pull requests are listed.

```bash
gh release create 0.2.25 --generate-notes
gh workflow run nuget.yml
```
