using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;

namespace Jinaga.Notebooks;

public static class JinagaClientExtensions
{
    private static readonly GraphvizRenderer _renderer = new GraphvizRenderer();

    public static HtmlString RenderFacts(this JinagaClient jinagaClient, params object[] projections)
    {
        string dot = Dot.JinagaClientExtensions.RenderFacts(jinagaClient, projections);
        return _renderer.RenderGraph(dot);
    }

    public static HtmlString RenderTypes(this JinagaClient jinagaClient, params Type[] types)
    {
        return Renderer.RenderTypes(types);
    }
}
