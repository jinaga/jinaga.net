using System;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Projections;

namespace Jinaga.Test.Specifications;

/// <summary>
/// The order of a compound projection's components reaches results: it is the order of the feeds
/// the projection produces, and the order its descriptive string prints them in. That order is a
/// property of the type, so a caller that enumerates the components receives it without sorting.
/// These build projections directly, as one loaded from elsewhere would arrive.
/// </summary>
public class CompoundProjectionTest
{
    private static readonly string[] NamesOutOfOrdinalOrder = { "b", "B", "a", "_x" };

    [Fact]
    public void EnumeratesComponentsInOrdinalOrder()
    {
        var compound = Compound(NamesOutOfOrdinalOrder);

        compound.Names.Should().Equal(Ordinal(NamesOutOfOrdinalOrder));
    }

    [Fact]
    public void KeepsOrdinalOrderThroughApply()
    {
        var compound = Compound(NamesOutOfOrdinalOrder);

        var applied = (CompoundProjection)compound.Apply(
            NamesOutOfOrdinalOrder.ToImmutableDictionary(
                name => Tag(name),
                name => $"renamed_{name}"));

        applied.Names.Should().Equal(Ordinal(NamesOutOfOrdinalOrder));
    }

    private static CompoundProjection Compound(string[] names) =>
        new CompoundProjection(
            names.ToImmutableDictionary(
                name => name,
                name => (Projection)new SimpleProjection(Tag(name), typeof(object))),
            typeof(object));

    private static string Tag(string name) => $"u_{name}";

    private static string[] Ordinal(string[] names) =>
        names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
}
