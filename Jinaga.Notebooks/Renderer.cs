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

    // Same fact-type graph as RenderTypes, with deletion markers folded into the fact they mark.
    // A fact "X" whose only marker successor is "X.Deleted" (no other predecessors or successors)
    // is filled orange, and "X.Deleted" is omitted. It is filled greenyellow when that deletion's
    // only successor is "X.Restored" (no other predecessors or successors); both are omitted.
    // Pass the marker types along with the facts:
    // Renderer.RenderTypesCompact(typeof(Tenant), typeof(InspectorDeleted), typeof(InspectorRestored), ...);

    public static HtmlString RenderTypesCompact(params Type[] types)
    {
        string graph = Dot.Renderer.RenderTypesCompact(types);
        return GraphvizRenderer.RenderGraph(graph);
    }
}
