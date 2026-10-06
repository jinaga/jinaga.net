namespace Jinaga.Notebooks.Dot;

public static class JinagaClientExtensions
{
    /// <summary>
    /// The graph of the facts found within the projections given, and the
    /// predecessors they reach.
    /// </summary>
    public static string RenderFacts(this JinagaClient jinagaClient, params object[] projections)
    {
        return InstanceGraph.Discover(jinagaClient, projections).ToDot();
    }
}
