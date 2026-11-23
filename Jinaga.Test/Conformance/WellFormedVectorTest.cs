using System.Collections.Generic;
using Jinaga.Projections;

namespace Jinaga.Test.Conformance;

/// <summary>
/// The well-formedness vectors of jinaga-spec: the checks accept a vector's specification
/// exactly when the spec's oracles, <c>isWellFormed</c> and <c>isConnectedDeep</c>, do.
///
/// Every vector states both verdicts, and they are independent: a specification that is not
/// well formed may be disconnected too, and one that is well formed may still be a product.
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

    /// <summary>
    /// The connectivity check of <c>ConnectedDescendants</c>, the hypothesis the feeds
    /// theorems take (jinaga/jinaga-spec#57). The oracle states it for every vector, well
    /// formed or not, so the check runs on specifications the other refusals would stop
    /// first.
    /// </summary>
    [Theory]
    [MemberData(nameof(Vectors))]
    public void TheConnectivityCheckAgreesWithTheOracle(string name)
    {
        var vector = ConformanceVector.Load("well-formed", name);

        var disconnection = ConnectedSpecification.Disconnect(vector.Specification);

        (disconnection == null).Should().Be(
            vector.Expected.GetProperty("connected").GetBoolean(),
            "the oracle says so, and the check reported [{0}]",
            disconnection?.Message ?? "connected");
    }
}
