using System.Collections.Generic;
using Jinaga.Projections;

namespace Jinaga.Test.Conformance;

/// <summary>
/// The well-formedness vectors of jinaga-spec: the check accepts a vector's specification
/// exactly when the spec's oracle, <c>isWellFormed</c>, does.
/// </summary>
public class WellFormedVectorTest
{
    public static IEnumerable<object[]> Vectors => ConformanceVector.Names("well-formed");

    [Theory]
    [MemberData(nameof(Vectors))]
    public void TheCheckAgreesWithTheOracle(string name)
    {
        var vector = ConformanceVector.Load("well-formed", name);

        var errors = WellFormedSpecification.Errors(vector.Specification);

        errors.IsEmpty.Should().Be(
            vector.Expected.GetProperty("wellFormed").GetBoolean(),
            "the oracle says so, and the check reported [{0}]", string.Join(" ", errors));
    }
}
