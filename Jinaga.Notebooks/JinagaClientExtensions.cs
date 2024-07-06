using System;
using Microsoft.AspNetCore.Html;

namespace Jinaga.Notebooks;

public static class JinagaClientExtensions
{
    public static HtmlString RenderFacts(this JinagaClient jinagaClient, params object[] projections)
    {
        string dot = Dot.JinagaClientExtensions.RenderFacts(jinagaClient, projections);
        return GraphvizRenderer.RenderGraph(dot);
    }

    public static HtmlString RenderTypes(this JinagaClient jinagaClient, params Type[] types)
    {
        return Renderer.RenderTypes(types);
    }
}
