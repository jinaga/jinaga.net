using Microsoft.AspNetCore.Html;
using System;

namespace Jinaga.Notebooks;

public static class Renderer
{
    public static HtmlString RenderTypes(params Type[] types)
    {
        string graph = Dot.Renderer.RenderTypes(types);
        return GraphvizRenderer.RenderGraph(graph);
    }
}
