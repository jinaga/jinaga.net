using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Xunit;

namespace Jinaga.Notebooks.Test;

public class GraphvizRendererTests
{
    [Fact]
    public void ShouldRenderSimpleGraph()
    {
        // Arrange
        var renderer = new GraphvizRenderer();
        var graph = "digraph { A -> B }";

        // Act
        var result = renderer.RenderGraph(graph);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<HtmlString>(result);
        var svg = result.ToString();
        Assert.Contains("<svg", svg);
        Assert.Contains("</svg>", svg);
        // The rendered SVG should contain our nodes
        Assert.Contains("A", svg);
        Assert.Contains("B", svg);
    }

    [Fact]
    public void ShouldHandleEmptyGraph()
    {
        // Arrange
        var renderer = new GraphvizRenderer();

        // Act
        var result = renderer.RenderGraph("");

        // Assert
        Assert.NotNull(result);
        Assert.IsType<HtmlString>(result);
        var svg = result.ToString();
        Assert.Contains("<svg", svg);
        Assert.Contains("Error rendering graph", svg);
        Assert.Contains("Graph cannot be empty", svg);
    }

    [Fact]
    public void ShouldHandleInvalidGraph()
    {
        // Arrange
        var renderer = new GraphvizRenderer();
        var invalidGraph = "invalid { syntax";

        // Act
        var result = renderer.RenderGraph(invalidGraph);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<HtmlString>(result);
        var svg = result.ToString();
        Assert.Contains("<svg", svg);
        Assert.Contains("Error rendering graph", svg);
    }
}
