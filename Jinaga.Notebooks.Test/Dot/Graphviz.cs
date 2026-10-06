using System.Diagnostics;

namespace Jinaga.Notebooks.Test.Dot;

/// <summary>
/// The real Graphviz, run over a rendered document. Parsing DOT in the test
/// project says what a document means; this says whether Graphviz accepts it.
/// Graphviz is not a build dependency, so tests that need it are skipped where
/// it is not installed.
/// </summary>
public static class Graphviz
{
    public static bool IsInstalled { get; } = Accepts("digraph {}");

    /// <summary>
    /// Renders a DOT document to SVG, throwing if Graphviz rejects it.
    /// </summary>
    public static string ToSvg(string dot)
    {
        using var process = new Process();
        process.StartInfo.FileName = "dot";
        process.StartInfo.Arguments = "-Tsvg";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.Start();

        // Drain both output pipes concurrently. Reading one to the end before
        // touching the other deadlocks as soon as Graphviz fills the pipe it is
        // not being read from: it blocks writing, so the stream being read
        // never reaches end of file. Graphviz warns once per unsupported style,
        // which is enough to fill that buffer on a graph of a few thousand
        // nodes.
        var output = Task.Run(() => process.StandardOutput.ReadToEnd());
        var errorOutput = Task.Run(() => process.StandardError.ReadToEnd());
        process.StandardInput.Write(dot);
        process.StandardInput.Close();
        var svg = output.Result;
        var error = errorOutput.Result;
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Graphviz rejected the document: {error.Trim()}\n{dot}");
        }
        return svg;
    }

    private static bool Accepts(string dot)
    {
        try
        {
            ToSvg(dot);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// A fact that needs Graphviz installed, and is skipped where it is not.
/// </summary>
public sealed class GraphvizFactAttribute : FactAttribute
{
    public GraphvizFactAttribute()
    {
        if (!Graphviz.IsInstalled)
        {
            Skip = "Graphviz is not installed";
        }
    }
}
