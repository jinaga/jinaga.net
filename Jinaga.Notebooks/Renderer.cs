using Microsoft.AspNetCore.Html;
using System;
using System.Threading.Tasks;

namespace Jinaga.Notebooks;

public static class Renderer
{
    public static Task<HtmlString> RenderTypes(params Type[] types)
    {
        string graph = Dot.Renderer.RenderTypes(types);
        return GraphvizRenderer.RenderGraph(graph);
    }
}
