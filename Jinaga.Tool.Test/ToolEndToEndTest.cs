namespace Jinaga.Tool.Test;

/// <summary>
/// Runs the built tool against the build output of Jinaga.Tool.TestLibrary, a
/// class library built with default settings. These are the issue's acceptance
/// criteria as the author meets them, in a process of their own.
/// </summary>
public class ToolEndToEndTest
{
    [Theory]
    [InlineData("authorization", "authorization {")]
    [InlineData("distribution", "distribution {")]
    [InlineData("purge", "purge {")]
    [InlineData("policy", "authorization {")]
    public void PrintsRulesFromAClassLibraryBuiltWithDefaultSettings(string target, string expected)
    {
        RequireADependencyOutsideTheBuildOutput();

        var output = ToolProcess.Run(null, "print", target, BuildOutput.RulesAssemblyPath);

        output.Should().Contain(expected);
        output.Should().NotContain("Could not");
    }

    [Fact]
    public void ReadsTheRulesBeforeDeployingThem()
    {
        RequireADependencyOutsideTheBuildOutput();

        // Port 9 discards, so the deployment itself fails. What it fails at is
        // the point: reaching the endpoint at all means the rules were read out
        // of a class library whose dependencies are not beside it.
        var output = ToolProcess.Run(
            null, "deploy", "authorization", BuildOutput.RulesAssemblyPath,
            "http://127.0.0.1:9/policy", "secret");

        output.Should().NotContain("Could not load file or assembly");
        output.Should().NotContain("Could not find the assembly");
    }

    [Fact]
    public void PrintsRulesFromOutputThatCarriesItsOwnDependencies()
    {
        using var directory = new TemporaryDirectory();
        using var packagesFolder = new TemporaryDirectory();
        var rulesAssemblyPath = BuildOutput.CopyRulesLibraryOutput(directory.Path);

        // The layout an application, or a library with
        // CopyLocalLockFileAssemblies, produces: every dependency beside the
        // rules assembly. The empty packages folder leaves nowhere else to look.
        var resolver = new RulesAssemblyDependencyResolver(BuildOutput.RulesAssemblyPath);
        foreach (var name in resolver.PackagedAssemblyNames)
        {
            var path = resolver.Resolve(name);
            if (path != null)
            {
                File.Copy(path, directory.Combine(Path.GetFileName(path)), true);
            }
        }

        var output = ToolProcess.Run(packagesFolder.Path, "print", "authorization", rulesAssemblyPath);

        output.Should().Contain("authorization {");
        output.Should().NotContain("Could not");
    }

    [Fact]
    public void NamesThePackageThePathsTriedAndTheRemedyWhenItIsRestoredNowhere()
    {
        using var directory = new TemporaryDirectory();
        using var packagesFolder = new TemporaryDirectory();
        var rulesAssemblyPath = BuildOutput.CopyRulesLibraryOutput(directory.Path);

        var output = ToolProcess.Run(packagesFolder.Path, "print", "authorization", rulesAssemblyPath);

        output.Should().NotContain("authorization {");
        output.Should().Contain("Could not find the assembly");
        output.Should().Contain(packagesFolder.Path, "the error should name the path it tried");
        output.Should().Contain("CopyLocalLockFileAssemblies");
    }

    [Fact]
    public void NamesTheProjectDependencyTakenAwayFromTheBuildOutput()
    {
        using var directory = new TemporaryDirectory();
        var rulesAssemblyPath = BuildOutput.CopyRulesLibraryOutput(directory.Path);
        var jinaga = directory.Combine("Jinaga.dll");
        File.Delete(jinaga);

        var output = ToolProcess.Run(null, "print", "authorization", rulesAssemblyPath);

        output.Should().Contain("Could not find the assembly Jinaga,");
        output.Should().Contain(jinaga, "the error should name the path it tried");
        output.Should().Contain("CopyLocalLockFileAssemblies");
    }

    /// <summary>
    /// Guards every test that runs against the rules library's own build
    /// output. If that output ever carried the whole dependency closure, those
    /// tests would pass without the tool resolving anything.
    /// </summary>
    private static void RequireADependencyOutsideTheBuildOutput()
    {
        var resolver = new RulesAssemblyDependencyResolver(BuildOutput.RulesAssemblyPath);

        resolver.PackagedAssemblyNames
            .Where(name => !File.Exists(Path.Combine(BuildOutput.RulesLibraryDirectory, name + ".dll")))
            .Should().NotBeEmpty(
                "Jinaga.Tool.TestLibrary is built without CopyLocalLockFileAssemblies, so the " +
                "tool has to resolve at least one of its dependencies from the packages folder");
    }
}
