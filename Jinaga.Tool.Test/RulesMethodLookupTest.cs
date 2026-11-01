using System.Reflection;

namespace Jinaga.Tool.Test;

/// <summary>
/// The tool finds a rules method on JinagaConfig by name. These cover which
/// overload it selects, which is what decides whether a compiling
/// configuration can be read at all.
/// </summary>
public class RulesMethodLookupTest
{
    // Only ever quoted back in a message; nothing here reads the filesystem.
    private const string AssemblyPath = "/build/output/Rules.dll";

    [Fact]
    public void FindsTheParameterlessOverloadWhenABuilderSharesItsName()
    {
        var method = Program.FindRulesMethod(typeof(OverloadedConfig), "Authorization");

        method.Should().NotBeNull();
        method!.GetParameters().Should().BeEmpty();
        method.ReturnType.Should().Be(typeof(string));
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("Distribution")]
    [InlineData("Purge")]
    public void ReadsRulesFromAConfigThatOverloadsTheRuleName(string methodName)
    {
        // The expected value comes from the builder, so the assertion holds
        // only if the lookup invoked the description method that wraps it.
        var expected = DescribeFromBuilder(methodName);

        var rules = Program.GetRules(typeof(OverloadedConfig), methodName, AssemblyPath);

        rules.Should().Be(expected);
        rules.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("Distribution")]
    public void StillReadsRulesFromAConfigThatDeclaresOnlyTheDescription(string methodName)
    {
        var rules = Program.GetRules(typeof(ParameterlessOnlyConfig), methodName, AssemblyPath);

        rules.Should().StartWith(methodName.ToLowerInvariant());
    }

    [Fact]
    public void ReportsTheMissingDescriptionWhenOnlyABuilderIsDeclared()
    {
        // A builder is not a description, so the lookup must not offer it.
        Program.FindRulesMethod(typeof(ParameterizedOnlyConfig), "Authorization").Should().BeNull();

        var reading = () => Program.GetRules(typeof(ParameterizedOnlyConfig), "Authorization", AssemblyPath);

        // The defect this guards is an AmbiguousMatchException escaping the
        // lookup, so the exception type carries the assertion.
        var thrown = reading.Should().Throw<ArgumentException>().Which;
        thrown.Should().NotBeOfType<AmbiguousMatchException>();
        thrown.Message.Should().Contain("Authorization").And.Contain(AssemblyPath);
    }

    [Fact]
    public void ReportsARuleTheConfigDoesNotDeclareAtAll()
    {
        var reading = () => Program.GetRules(typeof(ParameterlessOnlyConfig), "Purge", AssemblyPath);

        var thrown = reading.Should().Throw<ArgumentException>().Which;
        thrown.Should().NotBeOfType<AmbiguousMatchException>();
        thrown.Message.Should().Contain("Purge").And.Contain(AssemblyPath);
    }

    [Fact]
    public void ReadsEveryRuleIntoThePolicyOfAConfigThatOverloadsTheRuleNames()
    {
        var policy = Program.GetPolicy(typeof(OverloadedConfig), AssemblyPath);

        // One lookup serves print, deploy and policy, so the policy has to
        // carry exactly what the rules commands read for each of the three.
        foreach (var methodName in new[] { "Authorization", "Distribution", "Purge" })
        {
            policy.Should().Contain(Program.GetRules(typeof(OverloadedConfig), methodName, AssemblyPath).Trim());
        }
    }

    [Fact]
    public void LeavesOutOfThePolicyOnlyTheRulesTheConfigNeverDeclared()
    {
        var policy = Program.GetPolicy(typeof(ParameterlessOnlyConfig), AssemblyPath);

        policy.Should().Contain(Program.GetRules(typeof(ParameterlessOnlyConfig), "Authorization", AssemblyPath).Trim());
        policy.Should().Contain(Program.GetRules(typeof(ParameterlessOnlyConfig), "Distribution", AssemblyPath).Trim());
        policy.Should().NotContain("purge");
    }

    [Fact]
    public void RefusesAPolicyWhoseRuleIsDeclaredWithoutADescription()
    {
        // Treating a declared-but-unreadable rule as absent would deploy a
        // policy that silently omits rules the author did write.
        var reading = () => Program.GetPolicy(typeof(ParameterizedOnlyConfig), AssemblyPath);

        reading.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("Authorization");
    }

    private static string DescribeFromBuilder(string methodName) => methodName switch
    {
        "Authorization" => AuthorizationRules.Describe(OverloadedConfig.Authorization),
        "Distribution" => DistributionRules.Describe(OverloadedConfig.Distribution),
        "Purge" => PurgeConditions.Describe(OverloadedConfig.Purge),
        _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Not a rule name."),
    };
}
