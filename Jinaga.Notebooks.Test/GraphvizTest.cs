using System.Xml.Linq;
using Jinaga.Notebooks.Test.Dot;

namespace Jinaga.Notebooks.Test;

/// <summary>
/// What the renderers emit has to be DOT that Graphviz draws as the graph the
/// document means. Graphviz accepting a document is not enough on its own: a
/// name that ends its own quoted string is accepted and drawn as several nodes.
/// So the nodes Graphviz draws are compared against the nodes the document
/// describes. Skipped where Graphviz is not installed.
/// </summary>
public class GraphvizTest
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private readonly JinagaClient j = JinagaTest.Create();

    [GraphvizFact]
    public void EveryRenderedDocumentIsDrawnAsSvg()
    {
        foreach (var (description, dot) in Fixtures.All(j))
        {
            var svg = Graphviz.ToSvg(dot);

            var document = XDocument.Parse(svg);
            document.Root?.Name.LocalName.Should().Be("svg",
                "the document for {0} should be drawn as SVG", description);
        }
    }

    [GraphvizFact]
    public void GraphvizDrawsTheNodesTheDocumentDescribes()
    {
        foreach (var (description, dot) in Fixtures.All(j))
        {
            var drawn = NodesDrawnBy(Graphviz.ToSvg(dot));

            drawn.Should().BeEquivalentTo(DotGraph.Parse(dot).Nodes.Keys,
                "Graphviz should draw one node per fact in {0}", description);
        }
    }

    private static IEnumerable<string> NodesDrawnBy(string svg)
    {
        return XDocument.Parse(svg)
            .Descendants(Svg + "g")
            .Where(group => (string?)group.Attribute("class") == "node")
            .Select(group => group.Element(Svg + "title")?.Value ?? "")
            .ToList();
    }
}
