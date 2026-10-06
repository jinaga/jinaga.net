using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;

namespace Jinaga.Notebooks;

static class GraphvizRenderer
{
    /// <summary>
    /// Draws a DOT document as SVG, throwing if Graphviz is missing or rejects
    /// the document. A failed run never returns, because an empty
    /// <see cref="HtmlString"/> renders as a blank cell that a notebook author
    /// cannot tell apart from a graph with nothing in it.
    /// </summary>
    /// <param name="graph">The DOT document to draw.</param>
    /// <param name="executable">
    /// The Graphviz executable to run. A test points this at a name that is not
    /// installed, so the missing-Graphviz path is covered on a machine that has
    /// Graphviz.
    /// </param>
    internal static HtmlString RenderGraph(string graph, string executable = "dot")
    {
        using (Process process = new Process())
        {
            process.StartInfo.FileName = executable;
            process.StartInfo.Arguments = "-Tsvg";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            try
            {
                process.Start();
            }
            catch (Win32Exception exception)
            {
                // Process.Start reports a name that is not on the PATH as a bare
                // Win32Exception that never mentions Graphviz, so the author is
                // told to install it rather than left to decode an errno.
                throw new InvalidOperationException(
                    $"Graphviz could not be started as \"{executable}\". " +
                    "Install Graphviz and make sure it is on the PATH.",
                    exception);
            }

            // Drain both output pipes concurrently. Reading one to the end before
            // touching the other deadlocks as soon as Graphviz fills the pipe it
            // is not being read from: it blocks writing, so the stream being read
            // never reaches end of file. Graphviz warns once per unsupported
            // style, which is enough to fill that buffer on a graph of a few
            // thousand nodes.
            var output = Task.Run(() => process.StandardOutput.ReadToEnd());
            var errorOutput = Task.Run(() => process.StandardError.ReadToEnd());

            try
            {
                process.StandardInput.Write(graph);
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // Graphviz can reject a document and exit while the document is
                // still being written, which breaks the pipe. The exit code and
                // stderr below say why it left, so that is the error to raise
                // rather than the broken pipe it left behind.
            }

            string svg = output.Result;
            string error = errorOutput.Result;
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Graphviz rejected the document: {error.Trim()}\n{graph}");
            }

            return new HtmlString(svg);
        }
    }
}
