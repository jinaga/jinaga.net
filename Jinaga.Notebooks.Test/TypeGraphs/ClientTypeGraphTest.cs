using System.Xml.Linq;
using Jinaga.Notebooks.Test.Dot;
using Jinaga.Notebooks.Test.Models;
using Jinaga.Repository;
using Microsoft.AspNetCore.Html;

namespace Jinaga.Notebooks.Test.TypeGraphs;

/// <summary>
/// A notebook author reaches a type graph through the client, writing
/// <c>j.RenderTypes(...)</c>, so both views have to be reachable that way. These
/// go through the client extensions and read the SVG Graphviz draws, which is
/// what a notebook shows. Skipped where Graphviz is not installed.
/// </summary>
public class ClientTypeGraphTest
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private readonly JinagaClient j = JinagaTest.Create();

    [GraphvizFact]
    public void CompactGraphThroughTheClientFoldsTheDeletionMarkerIn()
    {
        var drawn = NodesDrawnBy(j.RenderTypesCompact(typeof(Employee), typeof(EmployeeDeleted)));

        drawn.Should().Contain(typeof(Employee).FactTypeName());
        drawn.Should().NotContain(typeof(EmployeeDeleted).FactTypeName());
    }

    [GraphvizFact]
    public void FullGraphThroughTheClientDrawsTheDeletionMarker()
    {
        var drawn = NodesDrawnBy(j.RenderTypes(typeof(Employee), typeof(EmployeeDeleted)));

        drawn.Should().Contain(typeof(Employee).FactTypeName());
        drawn.Should().Contain(typeof(EmployeeDeleted).FactTypeName());
    }

    private static IEnumerable<string> NodesDrawnBy(HtmlString rendered)
    {
        return XDocument.Parse(rendered.Value ?? "")
            .Descendants(Svg + "g")
            .Where(group => (string?)group.Attribute("class") == "node")
            .Select(group => group.Element(Svg + "title")?.Value ?? "")
            .ToList();
    }
}
