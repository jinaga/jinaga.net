using System.Collections.Generic;
using System.Text.Json;
using Jinaga.Projections;

namespace Jinaga.Test.Conformance;

/// <summary>
/// The split vectors of jinaga-spec: the split of a vector's specification prints the head and
/// tail the spec's oracle computes, and an authorization rule is refused exactly when its tail
/// would read the fact under authorization.
/// </summary>
public class SplitVectorTest
{
    public static IEnumerable<object[]> Vectors => ConformanceVector.Names("split");

    [Theory]
    [MemberData(nameof(Vectors))]
    public void TheLoaderReadsTheSpecificationTheVectorDescribes(string name)
    {
        var vector = ConformanceVector.Load("split", name);

        Describe(vector.Specification).Should().Be(vector.Text);
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void TheSplitAgreesWithTheOracle(string name)
    {
        var vector = ConformanceVector.Load("split", name);

        var (head, tail) = WellFormedSpecification.Check(vector.Specification, "The specification")
            .SplitBeforeFirstSuccessor();

        Describe(head).Should().Be(vector.Expected.GetProperty("headText").GetString());
        (tail is null ? null : Describe(tail)).Should().Be(TextOrNull(vector.Expected.GetProperty("tailText")));
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void AnAuthorizationRuleIsRefusedExactlyWhenItsTailReadsTheGiven(string name)
    {
        var vector = ConformanceVector.Load("split", name);
        var specification = vector.Specification;
        if (specification.Givens.Count != 1 || specification.Projection is not SimpleProjection)
        {
            // Only a specification with one given and a single projected label can be a rule.
            return;
        }

        var building = () => new AuthorizationRuleSpecification(specification);

        if (vector.Expected.GetProperty("tailReadsGiven").GetBoolean())
        {
            building.Should().Throw<InvalidOperationException>()
                .WithMessage($"*reads '{specification.Givens[0].Label.Name}' from the store*");
        }
        else
        {
            building.Should().NotThrow();
        }
    }

    private static string? TextOrNull(JsonElement json) =>
        json.ValueKind == JsonValueKind.Null ? null : json.GetString();

    private static string Describe(Specification specification) =>
        specification.ToDescriptiveString().ReplaceLineEndings("\n");
}
