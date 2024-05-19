using Microsoft.AspNetCore.Html;

namespace Jinaga.Graphviz;

public static class JinagaClientExtensions
{
    public static HtmlString RenderFacts(this JinagaClient jinagaClient, params object[] projections)
    {
        string dot = Dot.JinagaClientExtensions.RenderFacts(jinagaClient, projections);
        return GraphvizRenderer.RenderGraph(dot);
    }
}
