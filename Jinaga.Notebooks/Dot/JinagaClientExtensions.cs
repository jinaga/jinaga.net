namespace Jinaga.Notebooks.Dot;

public static class JinagaClientExtensions
{
    /// <summary>
    /// The graph of the facts found within the projections given, and the
    /// predecessors they reach, showing as much of each fact as the default
    /// options allow.
    /// </summary>
    public static string RenderFacts(this JinagaClient jinagaClient, params object[] projections)
    {
        return InstanceGraph.Discover(jinagaClient, projections).ToDot();
    }

    /// <summary>
    /// The graph of the facts found within the projections given, and the
    /// predecessors they reach, showing as much of each fact as the options say.
    /// </summary>
    public static string RenderFacts(this JinagaClient jinagaClient, InstanceGraphOptions options, params object[] projections)
    {
        return InstanceGraph.Discover(jinagaClient, options, projections).ToDot();
    }
}
