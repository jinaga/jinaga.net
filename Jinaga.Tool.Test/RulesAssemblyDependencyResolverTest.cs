namespace Jinaga.Tool.Test;

/// <summary>
/// Exercises the resolver against the real build output of
/// Jinaga.Tool.TestLibrary and the real NuGet global packages folder. Nothing
/// here describes the shape of a deps file: the SDK writes the one these tests
/// read, so a change to that format, or to how this project parses it, turns
/// them red.
/// </summary>
public class RulesAssemblyDependencyResolverTest
{
    [Fact]
    public void FindsEveryPackagedAssemblyInTheGlobalPackagesFolder()
    {
        var resolver = new RulesAssemblyDependencyResolver(BuildOutput.RulesAssemblyPath);

        resolver.PackagedAssemblyNames.Should().NotBeEmpty(
            "a class library built with default settings leaves its NuGet dependencies " +
            "in the packages folder and records them in its deps file");

        foreach (var name in resolver.PackagedAssemblyNames)
        {
            var path = resolver.Resolve(name);

            path.Should().NotBeNull($"the deps file lists a runtime asset for {name}");
            File.Exists(path).Should().BeTrue($"{path} should be a restored package asset");
            Path.GetFileNameWithoutExtension(path).Should().Be(name);

            if (!File.Exists(Path.Combine(BuildOutput.RulesLibraryDirectory, name + ".dll")))
            {
                path.Should().StartWith(RulesAssemblyDependencyResolver.DefaultPackagesFolder());
            }
        }
    }

    [Fact]
    public void PrefersACopyBesideTheRulesAssemblyOverThePackagesFolder()
    {
        using var directory = new TemporaryDirectory();
        var rulesAssemblyPath = BuildOutput.CopyRulesLibraryOutput(directory.Path);

        var name = OnlyInThePackagesFolder();
        var packaged = new RulesAssemblyDependencyResolver(BuildOutput.RulesAssemblyPath)
            .Resolve(name)!;
        var beside = directory.Combine(name + ".dll");
        File.Copy(packaged, beside);

        var resolver = new RulesAssemblyDependencyResolver(rulesAssemblyPath);
        var probePaths = resolver.ProbePaths(name);

        probePaths.Should().HaveCount(2, "both places hold a copy now");
        probePaths[0].Should().Be(beside);
        probePaths[1].Should().StartWith(RulesAssemblyDependencyResolver.DefaultPackagesFolder());
        resolver.Resolve(name).Should().Be(beside);
    }

    [Fact]
    public void NamesTheAssemblyThePathsTriedAndTheRemedyWhenNothingResolves()
    {
        using var packagesFolder = new TemporaryDirectory();
        var resolver = new RulesAssemblyDependencyResolver(
            BuildOutput.RulesAssemblyPath, packagesFolder.Path);
        var name = OnlyInThePackagesFolder();

        resolver.Resolve(name).Should().BeNull("the packages folder is empty");

        var failure = resolver.DescribeFailure(name);

        failure.Should().Contain(name);
        foreach (var probePath in resolver.ProbePaths(name))
        {
            failure.Should().Contain(probePath);
        }
        failure.Should().Contain("CopyLocalLockFileAssemblies");
    }

    [Fact]
    public void LooksOnlyBesideTheRulesAssemblyWhenThereIsNoDependencyFile()
    {
        using var directory = new TemporaryDirectory();
        var resolver = new RulesAssemblyDependencyResolver(directory.Combine("Rules.dll"));

        resolver.PackagedAssemblyNames.Should().BeEmpty();
        resolver.ProbePaths("Jinaga").Should().Equal(directory.Combine("Jinaga.dll"));
        resolver.DescribeFailure("Jinaga").Should().Contain(resolver.DependencyFilePath);
    }

    /// <summary>
    /// An assembly the rules library needs that its default build output does
    /// not carry. That is the case the resolver exists for, and a build that
    /// stopped producing one would leave these tests proving nothing, so this
    /// fails rather than returning nothing.
    /// </summary>
    private static string OnlyInThePackagesFolder()
    {
        var resolver = new RulesAssemblyDependencyResolver(BuildOutput.RulesAssemblyPath);
        var name = resolver.PackagedAssemblyNames.FirstOrDefault(packaged =>
            !File.Exists(Path.Combine(BuildOutput.RulesLibraryDirectory, packaged + ".dll")));

        name.Should().NotBeNull(
            "Jinaga.Tool.TestLibrary is built without CopyLocalLockFileAssemblies, so at " +
            "least one of its dependencies should be absent from its output");

        return name!;
    }
}
