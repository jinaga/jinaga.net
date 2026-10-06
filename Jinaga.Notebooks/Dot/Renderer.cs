using System;

namespace Jinaga.Notebooks.Dot;

public static class Renderer
{
    /// <summary>
    /// The graph of the fact types given and the predecessors they reach.
    /// </summary>
    public static string RenderTypes(params Type[] types)
    {
        return TypeGraph.Discover(types).ToDot();
    }

    /// <summary>
    /// The same graph as <see cref="RenderTypes"/>, with each deletion marker
    /// folded into the fact it marks.
    /// </summary>
    public static string RenderTypesCompact(params Type[] types)
    {
        return TypeGraph.Discover(types).CollapseDeletion().ToDot();
    }
}
