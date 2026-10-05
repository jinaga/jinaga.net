using Jinaga.Notebooks.Test.Dot;
using Jinaga.Repository;
using static Jinaga.Notebooks.Dot.Renderer;

namespace Jinaga.Notebooks.Test.TypeGraphs;

/// <summary>
/// A rendered fact-type graph, read for what it shows: which fact types appear,
/// which predecessors connect them, and how each fact type is marked.
/// How those meanings are spelled in DOT is known only here.
/// </summary>
public sealed class RenderedTypeGraph
{
    public DotGraph Dot { get; }

    private RenderedTypeGraph(DotGraph dot)
    {
        Dot = dot;
    }

    public static RenderedTypeGraph Full(params Type[] types) =>
        new(DotGraph.Parse(RenderTypes(types)));

    public static RenderedTypeGraph Compact(params Type[] types) =>
        new(DotGraph.Parse(RenderTypesCompact(types)));

    public RenderedFactType ShouldShow<TFact>() => ShouldShow(typeof(TFact));

    public RenderedFactType ShouldShow(Type factType)
    {
        var name = factType.FactTypeName();
        Dot.Nodes.Should().ContainKey(name, "the graph should show {0}", name);
        return new RenderedFactType(name, Dot.Nodes[name]);
    }

    public void ShouldNotShow<TFact>()
    {
        var name = typeof(TFact).FactTypeName();
        Dot.Nodes.Should().NotContainKey(name, "the graph should not show {0}", name);
    }

    public void ShouldShowExactly(params Type[] factTypes)
    {
        Dot.Nodes.Keys.Should().BeEquivalentTo(factTypes.Select(type => type.FactTypeName()));
    }

    public void ShouldShowPredecessor<TSuccessor, TPredecessor>(
        string role,
        Cardinality cardinality = Cardinality.One)
    {
        var successor = typeof(TSuccessor).FactTypeName();
        var predecessor = typeof(TPredecessor).FactTypeName();
        PredecessorsOf(successor)
            .Where(edge => edge.Predecessor == predecessor && edge.Role == role)
            .Select(edge => edge.Cardinality)
            .Should().Equal(new[] { cardinality },
                "{0} should show exactly one {1} predecessor {2} of cardinality {3}",
                successor, role, predecessor, cardinality);
    }

    public void ShouldShowNoPredecessorsOf<TFact>()
    {
        var name = typeof(TFact).FactTypeName();
        PredecessorsOf(name).Should().BeEmpty("{0} should show no predecessors", name);
    }

    public void ShouldShowSameGraphAs(RenderedTypeGraph other)
    {
        Dot.Nodes.Should().BeEquivalentTo(other.Dot.Nodes);
        Dot.Edges.Should().BeEquivalentTo(other.Dot.Edges);
    }

    /// <summary>
    /// This graph shows a subset of what the full graph shows: every fact type and
    /// predecessor it shows is in the full graph, and every fact type it leaves out
    /// is a deletion or restoration marker.
    /// </summary>
    public void ShouldBeAViewOf(RenderedTypeGraph full)
    {
        Dot.Nodes.Keys.Should().BeSubsetOf(full.Dot.Nodes.Keys);
        full.Dot.Nodes.Keys.Except(Dot.Nodes.Keys)
            .Where(name => !name.EndsWith(".Deleted") && !name.EndsWith(".Restored"))
            .Should().BeEmpty("only deletion and restoration markers may be left out");
        foreach (var edge in Dot.Edges)
        {
            full.Dot.Edges.Should().ContainEquivalentOf(edge);
        }
    }

    private IEnumerable<(string Predecessor, string Role, Cardinality Cardinality)> PredecessorsOf(string successor)
    {
        foreach (var edge in Dot.Edges.Where(edge => edge.From == successor))
        {
            var (role, cardinality) = RoleAndCardinality(edge);
            yield return (edge.To, role, cardinality);
        }
    }

    private static (string Role, Cardinality Cardinality) RoleAndCardinality(DotEdge edge)
    {
        var label = edge.Attributes["label"].Trim();
        return label switch
        {
            _ when label.EndsWith('*') => (label[..^1], Cardinality.Many),
            _ when label.EndsWith('?') => (label[..^1], Cardinality.Optional),
            _ => (label, Cardinality.One)
        };
    }
}

public sealed class RenderedFactType
{
    private readonly string name;
    private readonly IReadOnlyDictionary<string, string> attributes;

    internal RenderedFactType(string name, IReadOnlyDictionary<string, string> attributes)
    {
        this.name = name;
        this.attributes = attributes;
    }

    public RenderedFactType Unmarked()
    {
        attributes.Should().NotContainKey("fillcolor", "{0} should not be marked", name);
        return this;
    }

    public RenderedFactType MarkedDeletable() => MarkedWith("orange", "deletable");

    public RenderedFactType MarkedRestorable() => MarkedWith("greenyellow", "restorable");

    private RenderedFactType MarkedWith(string color, string meaning)
    {
        attributes.Should().Contain("style", "filled", "{0} should be marked {1}", name, meaning);
        attributes.Should().Contain("fillcolor", color, "{0} should be marked {1}", name, meaning);
        return this;
    }
}
