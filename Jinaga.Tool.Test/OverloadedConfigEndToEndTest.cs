using System.Reflection;

namespace Jinaga.Tool.Test;

/// <summary>
/// Runs the built tool against Jinaga.Tool.TestLibrary, whose JinagaConfig
/// declares each rules builder beside the method that describes it. These are
/// issue #347's acceptance criteria as the author meets them: the tool is a
/// process of its own, reading a class library built the ordinary way.
/// </summary>
public class OverloadedConfigEndToEndTest
{
    [Theory]
    [InlineData("print", "authorization", "authorization {")]
    [InlineData("print", "distribution", "distribution {")]
    [InlineData("print", "purge", "purge {")]
    [InlineData("print", "policy", "authorization {")]
    public void ReadsRulesPastTheBuilderSharingTheirName(string command, string target, string expected)
    {
        RequireTheRulesBuildersToBeOverloads();

        var output = ToolProcess.Run(null, command, target, BuildOutput.RulesAssemblyPath);

        output.Should().Contain(expected);
        output.Should().NotContain("Ambiguous match found");
    }

    [Fact]
    public void ReadsEveryRuleIntoThePolicy()
    {
        RequireTheRulesBuildersToBeOverloads();

        var output = ToolProcess.Run(null, "print", "policy", BuildOutput.RulesAssemblyPath);

        output.Should().Contain("authorization {");
        output.Should().Contain("distribution {");
        output.Should().Contain("purge {");
        output.Should().NotContain("Ambiguous match found");
    }

    [Fact]
    public void ReadsTheRulesBeforeDeployingThem()
    {
        RequireTheRulesBuildersToBeOverloads();

        // Port 9 discards, so the deployment fails at the endpoint. Failing
        // there is the point: the rules were read first.
        var output = ToolProcess.Run(
            null, "deploy", "authorization", BuildOutput.RulesAssemblyPath,
            "http://127.0.0.1:9/policy", "secret");

        output.Should().NotContain("Ambiguous match found");
        output.Should().NotContain("Expected method JinagaConfig.Authorization");
    }

    /// <summary>
    /// Guards every test above. The defect needs a public builder sharing a
    /// rule's name to appear at all, so a fixture whose builders went back to
    /// being private would let these pass while proving nothing.
    /// </summary>
    private static void RequireTheRulesBuildersToBeOverloads()
    {
        // Read the built rules assembly rather than naming its types: the
        // project reference to it carries ReferenceOutputAssembly="false", so
        // the compiler never sees them.
        var config = Assembly.LoadFrom(BuildOutput.RulesAssemblyPath)
            .GetTypes()
            .Single(type => type.Name == "JinagaConfig");

        foreach (var name in new[] { "Authorization", "Distribution", "Purge" })
        {
            config.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(method => method.Name == name)
                .Should().HaveCountGreaterThan(1,
                    $"JinagaConfig.{name} needs a public builder beside its description " +
                    "for the lookup to have anything to disambiguate");
        }
    }
}
