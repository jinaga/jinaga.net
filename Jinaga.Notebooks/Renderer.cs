﻿using Microsoft.AspNetCore.Html;
using System;
using System.Threading.Tasks;

namespace Jinaga.Notebooks;

public static class Renderer
{
    private static readonly GraphvizRenderer _renderer = new GraphvizRenderer();

    public static Task<HtmlString> RenderTypes(params Type[] types)
    {
        string graph = Dot.Renderer.RenderTypes(types);
        return _renderer.RenderGraph(graph);
    }
}
