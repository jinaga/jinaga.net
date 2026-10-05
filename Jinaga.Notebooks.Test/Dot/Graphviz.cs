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

        process.StandardInput.Write(dot);
        process.StandardInput.Close();
        var svg = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
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
