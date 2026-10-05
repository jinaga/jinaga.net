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

    // Same fact-type graph as RenderTypes. A lone …Deleted successor (no other predecessors or
    // successors) turns its fact orange, and that …Deleted fact is omitted. It is greenyellow when
    // that deletion's only successor is a lone …Restored; both facts are omitted.
    // In a notebook: Renderer.RenderTypesCompact(typeof(Tenant), typeof(InspectorDeleted), typeof(InspectorRestored), ...);

    public static HtmlString RenderTypesCompact(params Type[] types)
    {
        string graph = Dot.Renderer.RenderTypesCompact(types);
        return GraphvizRenderer.RenderGraph(graph);
    }
}
