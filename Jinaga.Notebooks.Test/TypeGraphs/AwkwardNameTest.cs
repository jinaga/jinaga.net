using Jinaga.Notebooks.Test.Models;

namespace Jinaga.Notebooks.Test.TypeGraphs;

public class AwkwardNameTest
{
    [Fact]
    public void FactTypeNameContainingAQuoteIsShownAsWritten()
    {
        var graph = RenderedTypeGraph.Full(typeof(Quoted));

        graph.ShouldShowExactly(typeof(Quoted));
    }

    [Fact]
    public void AnEdgeToAFactTypeNamedWithAQuoteIsShownAsWritten()
    {
        var graph = RenderedTypeGraph.Full(typeof(Child));

        graph.ShouldShowPredecessor<Child, Quoted>(nameof(Child.quoted));
        graph.ShouldShowPredecessor<Child, Ampersand>(nameof(Child.ampersand));
        graph.ShouldShowPredecessor<Child, Unicode>(nameof(Child.unicode));
    }

    [Fact]
    public void CompactGraphShowsAnAwkwardNameTheSameWay()
    {
        RenderedTypeGraph.Compact(typeof(Child))
            .ShouldShowSameGraphAs(RenderedTypeGraph.Full(typeof(Child)));
    }
}
