namespace Jinaga.Notebooks.Test.Dot;

/// <summary>
/// Whether the real Graphviz is available to this test run. Parsing DOT in the
/// test project says what a document means; drawing it through
/// <see cref="GraphvizRenderer"/>, the renderer the package itself uses, says
/// whether Graphviz accepts it. Graphviz is not a build dependency, so tests
/// that need it are skipped where it is not installed.
/// </summary>
public static class Graphviz
{
    public static bool IsInstalled { get; } = Accepts("digraph {}");

    private static bool Accepts(string dot)
    {
        try
        {
            GraphvizRenderer.RenderGraph(dot);
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
