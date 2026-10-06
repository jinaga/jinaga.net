using System.ComponentModel;
using Jinaga.Notebooks.Test.Dot;

namespace Jinaga.Notebooks.Test;

/// <summary>
/// A dot run that fails has to say so. An empty <c>HtmlString</c> renders as a
/// blank cell, which a notebook author cannot tell apart from a graph that drew
/// nothing, so both failures are checked here: Graphviz rejecting the document,
/// and Graphviz not being installed at all.
/// </summary>
public class GraphvizRendererTest
{
    /// <summary>
    /// Needs Graphviz, because the message asserted on is Graphviz's own.
    /// Skipped where it is not installed, as <see cref="GraphvizTest"/> is.
    /// </summary>
    [GraphvizFact]
    public void ADocumentGraphvizRejectsIsReportedWithGraphvizsOwnMessage()
    {
        Action render = () => GraphvizRenderer.RenderGraph("digraph { a -> }");

        render.Should().Throw<InvalidOperationException>()
            .WithMessage("*syntax error*");
    }

    /// <summary>
    /// Runs everywhere: it names an executable no machine has, so it does not
    /// depend on whether Graphviz is installed.
    /// </summary>
    [Fact]
    public void AnExecutableThatIsNotInstalledIsReportedAsMissingGraphviz()
    {
        Action render = () => GraphvizRenderer.RenderGraph(
            "digraph {}", "dot-no-such-executable");

        render.Should().Throw<InvalidOperationException>()
            .WithMessage("*Graphviz*")
            .WithMessage("*PATH*")
            .WithInnerException<Win32Exception>();
    }
}
