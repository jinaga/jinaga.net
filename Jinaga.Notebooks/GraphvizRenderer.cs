using System;
using System.Threading.Tasks;
using Jint;
using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;

namespace Jinaga.Notebooks;

static class GraphvizRenderer
{
    private static readonly Engine JsEngine;
    private static readonly string ErrorSvgTemplate = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""500"" height=""60"">
        <text x=""10"" y=""30"" fill=""red"">Error rendering graph: {0}</text>
    </svg>";

    static GraphvizRenderer()
    {
        JsEngine = new Engine(options => {
            options.AllowClr();
        });

        // Load and execute Viz.js resources
        var vizJs = ResourceLoader.LoadVizJs();
        var vizRenderJs = ResourceLoader.LoadVizRenderJs();
        
        JsEngine.Execute(vizJs);
        JsEngine.Execute(vizRenderJs);
        
        // Initialize Viz instance
        JsEngine.Execute("var viz = new Viz();");
    }

    internal static async Task<HtmlString> RenderGraph(string graph)
    {
        try
        {
            // Escape the graph for JavaScript
            var escapedGraph = JavaScriptEncoder.Default.Encode(graph);
            
            // Set up completion callback
            var taskCompletionSource = new TaskCompletionSource<string>();
            
            JsEngine.SetValue("resolveCallback", (string svg) => taskCompletionSource.SetResult(svg));
            JsEngine.SetValue("rejectCallback", (string error) => taskCompletionSource.SetException(new Exception(error)));

            // Execute rendering with callbacks
            JsEngine.Evaluate($@"
                viz.renderString(`{escapedGraph}`)
                    .then(svg => resolveCallback(svg))
                    .catch(error => rejectCallback(error.message));
            ");

            // Wait for the callback to be called
            var svg = await taskCompletionSource.Task;
            return new HtmlString(svg.ToString());
        }
        catch (Exception ex)
        {
            // Return error message as SVG
            return new HtmlString(string.Format(ErrorSvgTemplate, 
                HtmlEncoder.Default.Encode(ex.Message)));
        }
    }
}
