using System.Diagnostics;
using Microsoft.AspNetCore.Html;

namespace Jinaga.Notebooks;

static class GraphvizRenderer
{
    internal static HtmlString RenderGraph(string graph)
    {
        using (Process process = new Process())
        {
            process.StartInfo.FileName = "dot";
            process.StartInfo.Arguments = "-Tsvg";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();

            process.StandardInput.Write(graph);
            process.StandardInput.Close();
            string svg = process.StandardOutput.ReadToEnd();

            process.WaitForExit();
            return new HtmlString(svg);
        }
    }
}