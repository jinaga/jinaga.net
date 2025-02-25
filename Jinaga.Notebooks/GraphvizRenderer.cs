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
            // Load and execute Viz.js resources
            var vizJs = ResourceLoader.LoadVizJs();
            var vizRenderJs = ResourceLoader.LoadVizRenderJs();
            
            Console.WriteLine($"Loaded viz.js ({vizJs.Length} chars)");
            Console.WriteLine($"Loaded full.render.js ({vizRenderJs.Length} chars)");
            
            // Set up console.log for JavaScript
            _jsEngine.SetValue("consoleLog", new Action<string>(msg => Console.WriteLine($"JS: {msg}")));
            _jsEngine.Execute("var console = { log: consoleLog };");
            
            // Set up global context
            _jsEngine.Execute(@"
                var global = this;
                global.Worker = function() {};  // Mock Worker
            ");

            // Execute viz.js in the global context
            _jsEngine.Execute($@"
                (function(global) {{
                    {vizJs}
                }})(global);
            ");

            // Execute render.js in the global context
            _jsEngine.Execute($@"
                (function(global) {{
                    {vizRenderJs}
                }})(global);
            ");
            
            // Initialize Viz instance and verify it exists
            _jsEngine.Execute(@"
                var viz = new Viz();
            ");
            var vizExists = _jsEngine.Evaluate("typeof viz !== 'undefined'").AsBoolean();
            if (!vizExists)
            {
                throw new InvalidOperationException("Viz instance was not created");
            }
            Console.WriteLine("Viz instance created successfully");
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
            
            // Set up completion callback
            var taskCompletionSource = new TaskCompletionSource<string>();
            
            _jsEngine.SetValue("resolveCallback", (string svg) => taskCompletionSource.SetResult(svg));
            _jsEngine.SetValue("rejectCallback", (string error) => taskCompletionSource.SetException(new Exception(error)));

            try
            {
                Console.WriteLine($"Attempting to render graph: {graph}");
                
                // Execute rendering with callbacks
                _jsEngine.Evaluate($@"
                    console.log('Starting render');
                    viz.renderString('{escapedGraph}')
                        .then(function(svg) {{
                            console.log('Render completed');
                            resolveCallback(svg);
                        }})
                        .catch(function(error) {{
                            console.log('Render failed: ' + error.toString());
                            rejectCallback(error.toString());
                        }});
                ");

                // Wait for the callback to be called with a timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                var completedTask = await Task.WhenAny(taskCompletionSource.Task, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("Graph rendering timed out after 10 seconds");
                }

                var svg = await taskCompletionSource.Task;
                Console.WriteLine($"Render result length: {svg?.Length ?? 0}");
                return new HtmlString(svg ?? string.Empty);
            }
            catch (Exception ex) when (ex is not TimeoutException)
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
