using System;
using System.Web;
using Microsoft.AspNetCore.Html;

namespace Jinaga.Notebooks;

static class GraphvizRenderer
{
    private static readonly string HtmlTemplate = @"
        <div id=""graph-{0}""></div>
        <script>
            {1}
            {2}
            (function() {{
                const viz = new Viz();
                const graphDiv = document.getElementById('graph-{0}');
                viz.renderSVGElement(`{3}`)
                    .then(element => {{
                        graphDiv.appendChild(element);
                    }})
                    .catch(error => {{
                        console.error(error);
                        graphDiv.innerHTML = `<pre style=""color: red"">Error rendering graph: ${{error.message}}</pre>`;
                    }});
            }})();
        </script>";

    internal static HtmlString RenderGraph(string graph)
    {
        // Generate a unique ID for this graph instance
        var uniqueId = Guid.NewGuid().ToString("N");
        
        // Load Viz.js resources
        var vizJs = ResourceLoader.LoadVizJs();
        var vizRenderJs = ResourceLoader.LoadVizRenderJs();
        
        // Escape the graph for JavaScript
        var escapedGraph = HttpUtility.JavaScriptStringEncode(graph);
        
        // Generate the complete HTML with embedded scripts
        var html = string.Format(
            HtmlTemplate,
            uniqueId,
            vizJs,
            vizRenderJs,
            escapedGraph
        );
        
        return new HtmlString(html);
    }
}
