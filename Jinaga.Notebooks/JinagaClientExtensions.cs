using System;
using Jinaga.Notebooks.Dot;
using Microsoft.AspNetCore.Html;

namespace Jinaga.Notebooks;

public static class JinagaClientExtensions
{
    public static HtmlString RenderFacts(this JinagaClient jinagaClient, params object[] projections)
    {
        string dot = Dot.JinagaClientExtensions.RenderFacts(jinagaClient, projections);
        return GraphvizRenderer.RenderGraph(dot);
    }

    /// <summary>
    /// The graph of the facts found within the projections given, showing as
    /// much of each fact as the options say.
    /// </summary>
    public static HtmlString RenderFacts(this JinagaClient jinagaClient, InstanceGraphOptions options, params object[] projections)
    {
        string dot = Dot.JinagaClientExtensions.RenderFacts(jinagaClient, options, projections);
        return GraphvizRenderer.RenderGraph(dot);
    }

    public static HtmlString RenderTypes(this JinagaClient jinagaClient, params Type[] types)
    {
        return Renderer.RenderTypes(types);
    }

    public static HtmlString RenderTypesCompact(this JinagaClient jinagaClient, params Type[] types)
    {
        return Renderer.RenderTypesCompact(types);
    }
}
