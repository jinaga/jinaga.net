namespace Jinaga.Notebooks.Test.Dot;

public class DotGraphTest
{
    [Fact]
    public void SpellingDoesNotChangeTheGraph()
    {
        var one = DotGraph.Parse(string.Join("\n",
            "digraph {",
            "    rankdir=BT",
            "    \"B\" [style=filled, fillcolor=orange]",
            "    \"B\" -> \"A\" [label=\" a\"]",
            "}"));
        var other = DotGraph.Parse(string.Join("\n",
            "digraph {",
            "  rankdir=BT",
            "  \"A\"",
            "  \"B\" -> \"A\" [label=\" a\"]",
            "  \"B\" [fillcolor=orange,style=filled]",
            "}"));

        other.Nodes.Should().BeEquivalentTo(one.Nodes);
        other.Edges.Should().BeEquivalentTo(one.Edges);
        other.GraphAttributes.Should().BeEquivalentTo(one.GraphAttributes);
    }

    [Fact]
    public void HtmlLabelIsReadWhole()
    {
        var graph = DotGraph.Parse(string.Join("\n",
            "digraph {",
            "    node [shape=none]",
            "    \"h\" [label=<<TABLE BORDER=\"0\"><TR><TD>x</TD></TR></TABLE>>]",
            "}"));

        graph.NodeDefaults["shape"].Should().Be("none");
        graph.Nodes["h"]["label"].Should().Be("<TABLE BORDER=\"0\"><TR><TD>x</TD></TR></TABLE>");
    }

    [Fact]
    public void UnrecognizedStatementIsRejected()
    {
        var parse = () => DotGraph.Parse(string.Join("\n",
            "digraph {",
            "    subgraph cluster_0 { \"A\" }",
            "}"));

        parse.Should().Throw<FormatException>();
    }
}
