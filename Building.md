# Building Jinaga.NET

To build Jinaga.NET locally, either use Visual Studio, or the command line.
Using the command line, execute the following commands from the root folder:

```bash
dotnet restore
dotnet build
dotnet test
```

## Versioning

This repository uses [NB.GV](https://github.com/dotnet/Nerdbank.GitVersioning) for versioning.
This allows each project to have its own version number.
Changing one project does not affect the version number of another projects.
Each project within this monorepo has a `version.json` file that filters the path while computing the git height.
The git height is used to determine the patch number.

### Feature Branches

When working on a feature branch, the version number will include the commit hash.
This differentiates feature builds from release builds.

### Release Servicing Branches

When preparing a new major or minor release, create a release servicing branch.
You can use the [`nbgv prepare-release`](https://github.com/dotnet/Nerdbank.GitVersioning/blob/main/doc/nbgv-cli.md) command to create the branch and update the version number.
This command will create a branch with the name `vx.y`.
Then on the current branch it will update the `version.json` file to the next version number.
It will append a `-alpha` suffix to the version number.
Remove this suffix when you are ready to publish stable releases within the new major or minor version.

To produce a new service release, merge your fixes into the servicing branch.
Builds from the servicing branch will have a stable version number.

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