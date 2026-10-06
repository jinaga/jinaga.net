using Jinaga.Notebooks.Dot;
using Jinaga.Notebooks.Test.Models;

namespace Jinaga.Notebooks.Test.TypeGraphs;

public class OptionalPredecessorTest
{
    [Fact]
    public void NullableSinglePredecessorIsOptional()
    {
        var graph = RenderedTypeGraph.Full(typeof(Site));

        graph.ShouldShowPredecessor<Site, Region>(nameof(Site.backup), Cardinality.Optional);
    }

    [Fact]
    public void SinglePredecessorThatIsNotNullableIsRequired()
    {
        var graph = RenderedTypeGraph.Full(typeof(Site));

        graph.ShouldShowPredecessor<Site, Region>(nameof(Site.region));
    }

    [Fact]
    public void CompactGraphShowsOptionalTheSameWay()
    {
        var graph = RenderedTypeGraph.Compact(typeof(Site));

        graph.ShouldShowPredecessor<Site, Region>(nameof(Site.backup), Cardinality.Optional);
        graph.ShouldShowPredecessor<Site, Region>(nameof(Site.region));
    }

    [Fact]
    public void WithoutNullableAnnotationsNoSinglePredecessorIsOptional()
    {
        var graph = RenderedTypeGraph.Full(typeof(Post));

        graph.ShouldShowPredecessor<Post, Zone>(nameof(Post.zone));
        graph.ShouldShowPredecessor<Post, Zone>(nameof(Post.backup));
    }

    [Fact]
    public void ArrayOfPredecessorsIsManyRatherThanOptional()
    {
        var graph = RenderedTypeGraph.Full(typeof(Assignment));

        graph.ShouldShowPredecessor<Assignment, Office>(nameof(Assignment.offices), Cardinality.Many);
    }
}
