using System.Text.RegularExpressions;
using System.Web;
using Jinaga.Notebooks.Test.Dot;
using Jinaga.Repository;

namespace Jinaga.Notebooks.Test.FactGraphs;

/// <summary>
/// A rendered graph of facts, read for what it shows: which facts appear, their
/// type and fields as displayed, which are highlighted as requested, and which
/// predecessors connect them. Facts are located by their hash, so a test names
/// the fact itself rather than its node identifier.
/// How those meanings are spelled in DOT and HTML labels is known only here.
/// </summary>
public sealed class RenderedFactGraph
{
    private readonly JinagaClient j;

    public DotGraph Dot { get; }

    private RenderedFactGraph(JinagaClient j, DotGraph dot)
    {
        this.j = j;
        Dot = dot;
    }

    public static RenderedFactGraph Of(JinagaClient j, params object[] projections) =>
        new(j, DotGraph.Parse(Jinaga.Notebooks.Dot.JinagaClientExtensions.RenderFacts(j, projections)));

    public RenderedFact ShouldShow(object fact)
    {
        var hash = HashOf(fact);
        Dot.Nodes.Should().ContainKey(hash, "the graph should show {0}", fact);
        return RenderedFact.Parse(fact, Dot.Nodes[hash]["label"]);
    }

    public void ShouldShowExactly(params object[] facts)
    {
        Dot.Nodes.Keys.Should().BeEquivalentTo(facts.Select(HashOf));
    }

    public void ShouldShowNothing()
    {
        Dot.Nodes.Should().BeEmpty();
    }

    public void ShouldShowPredecessor(object successor, string role, object predecessor)
    {
        var from = HashOf(successor);
        var to = HashOf(predecessor);
        Dot.Edges
            .Where(edge => edge.From == from && edge.To == to && edge.Attributes["label"].Trim() == role)
            .Should().ContainSingle("{0} should show {1} as its {2} predecessor once", successor, predecessor, role);
    }

    public void ShouldShowPredecessorCount(object successor, int count)
    {
        var from = HashOf(successor);
        Dot.Edges.Where(edge => edge.From == from).Should().HaveCount(count);
    }

    private string HashOf(object fact) => j.Graph(fact).Last.Hash;
}

public sealed class RenderedFact
{
    private static readonly Regex Table = new(
        "^<TABLE BORDER=\"(?<border>[01])\" CELLBORDER=\"1\" CELLSPACING=\"0\">" +
        "<TR><TD COLSPAN=\"2\">(?<type>[^<]*)</TD></TR>" +
        "(?<fields>(<TR><TD>[^<]*</TD><TD>[^<]*</TD></TR>)*)" +
        "</TABLE>$");
    private static readonly Regex Field = new("<TR><TD>(?<name>[^<]*)</TD><TD>(?<value>[^<]*)</TD></TR>");

    private readonly object fact;
    private readonly string type;
    private readonly bool highlighted;
    private readonly IReadOnlyList<(string Name, string Value)> fields;

    private RenderedFact(object fact, string type, bool highlighted, IReadOnlyList<(string, string)> fields)
    {
        this.fact = fact;
        this.type = type;
        this.highlighted = highlighted;
        this.fields = fields;
    }

    internal static RenderedFact Parse(object fact, string label)
    {
        var match = Table.Match(label);
        match.Success.Should().BeTrue("the label of {0} should be a table of its type and fields, but was {1}", fact, label);
        var fields = Field.Matches(match.Groups["fields"].Value)
            .Select(field => (
                Uri.UnescapeDataString(field.Groups["name"].Value),
                HttpUtility.HtmlDecode(field.Groups["value"].Value)))
            .ToList();
        return new RenderedFact(
            fact,
            Uri.UnescapeDataString(match.Groups["type"].Value),
            match.Groups["border"].Value == "1",
            fields);
    }

    public RenderedFact OfItsType()
    {
        type.Should().Be(fact.GetType().FactTypeName());
        return this;
    }

    public RenderedFact Highlighted()
    {
        highlighted.Should().BeTrue("{0} was requested, so it should be highlighted", fact);
        return this;
    }

    public RenderedFact NotHighlighted()
    {
        highlighted.Should().BeFalse("{0} was not requested, so it should not be highlighted", fact);
        return this;
    }

    public RenderedFact WithField(string name, string displayed)
    {
        fields.Should().ContainSingle(field => field.Name == name)
            .Which.Value.Should().Be(displayed, "field {0} of {1} should display as {2}", name, fact, displayed);
        return this;
    }

    public RenderedFact WithFields(params string[] names)
    {
        fields.Select(field => field.Name).Should().Equal(names);
        return this;
    }
}
