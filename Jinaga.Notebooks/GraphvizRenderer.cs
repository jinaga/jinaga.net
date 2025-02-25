using System;
using System.Threading.Tasks;
using Jint;
using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;

namespace Jinaga.Notebooks;

public class GraphvizRenderer
{
    private readonly Engine _jsEngine;
    private const string ErrorSvgTemplate = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""500"" height=""60"">
        <text x=""10"" y=""30"" fill=""red"">Error rendering graph: {0}</text>
    </svg>";

    public GraphvizRenderer(Engine? jsEngine = null)
    {
        _jsEngine = jsEngine ?? new Engine(options => {
            options.AllowClr();
        });

        Initialize();
    }

    private void Initialize()
    {
        try
        {
            // Load and execute Viz.js
            var vizJs = ResourceLoader.LoadVizJs();
            Console.WriteLine($"Loaded viz-standalone.js ({vizJs.Length} chars)");
            
            // Set up console.log for JavaScript
            _jsEngine.SetValue("consoleLog", new Action<string>(msg => Console.WriteLine($"JS: {msg}")));
            _jsEngine.Execute("var console = { log: consoleLog };");

            // Execute viz.js in the global context
            _jsEngine.Execute(vizJs);
            _jsEngine.Execute(@"
                if (typeof Viz === 'undefined') {
                    console.log('Viz function is not defined');
                    throw new Error('Viz function is not defined');
                }
                console.log('Viz function is available: ' + typeof Viz);

                // Create a wrapper function
                window.renderGraph = function(input) {
                    console.log('Rendering graph:', input);
                    var options = { format: 'svg', engine: 'dot' };
                    console.log('Using options:', JSON.stringify(options));
                    return Viz(input, options);
                };
            ");
            Console.WriteLine("Viz.js loaded successfully");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize Graphviz renderer", ex);
        }
    }

    public async Task<HtmlString> RenderGraph(string graph)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(graph))
            {
                throw new ArgumentException("Graph cannot be empty", nameof(graph));
            }

            // Escape the graph for JavaScript
            var escapedGraph = JavaScriptEncoder.Default.Encode(graph);
            
            try
            {
                Console.WriteLine($"Attempting to render graph: {graph}");
                
                // Set up the input
                _jsEngine.SetValue("graphInput", graph);

                // Execute rendering using wrapper function
                var result = _jsEngine.Evaluate(@"
                    try {
                        var result = window.renderGraph(graphInput);
                        if (!result) {
                            throw new Error('Render returned null or undefined');
                        }
                        if (!result.includes('<svg')) {
                            throw new Error('Invalid SVG output');
                        }
                        console.log('Render successful');
                        return result;
                    } catch (e) {
                        console.log('Render error:', e.message);
                        throw e;
                    }
                ").AsString();

                if (string.IsNullOrEmpty(result))
                {
                    throw new InvalidOperationException("Viz returned empty result");
                }

                Console.WriteLine($"Render result length: {result.Length}");
                return new HtmlString(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Render failed: {ex}");
                throw new InvalidOperationException("Failed to render graph", ex);
            }
        }
        catch (Exception ex)
        {
            // Return error message as SVG
            return new HtmlString(string.Format(ErrorSvgTemplate, 
                HtmlEncoder.Default.Encode(ex.Message)));
        }
    }
}
