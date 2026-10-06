using Jinaga.Notebooks.Dot;
using Jinaga.Notebooks.Test.Models;

namespace Jinaga.Notebooks.Test.TypeGraphs;

/// <summary>
/// The type graph read as a model: which fact types it holds, which roles
/// connect them, what cardinality each role has, and how each fact type is
/// marked. No DOT is written or parsed here.
/// </summary>
public class TypeGraphTest
{
    [Fact]
    public void AFactTypeWithoutPredecessorsIsASingleNode()
    {
        var graph = TypeGraph.Discover(typeof(Company));

        graph.Nodes.Select(node => node.FactClass).Should().Equal(typeof(Company));
        graph.Edges.Should().BeEmpty();
    }

    [Fact]
    public void EveryFactTypeStartsUnmarked()
    {
        var graph = TypeGraph.Discover(TypeCatalog.All);

        graph.Nodes.Should().OnlyContain(node => node.Marking == Marking.None);
    }

    [Fact]
    public void ANodeCarriesItsFactTypeName()
    {
        var graph = TypeGraph.Discover(typeof(Company));

        graph.Nodes.Select(node => node.Name).Should().Equal("Corporate.Company");
    }

    [Fact]
    public void ASinglePredecessorIsOneByRole()
    {
        var graph = TypeGraph.Discover(typeof(Office));

        graph.Edges.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                Successor = typeof(Office),
                Role = nameof(Office.company),
                Predecessor = typeof(Company),
                Cardinality = Cardinality.One
            });
    }

    [Fact]
    public void AnArrayOfPredecessorsIsMany()
    {
        var graph = TypeGraph.Discover(typeof(Assignment));

        CardinalityOf(graph, typeof(Assignment), nameof(Assignment.offices))
            .Should().Be(Cardinality.Many);
    }

    [Fact]
    public void APredecessorAnnotatedNullableIsOptional()
    {
        var graph = TypeGraph.Discover(typeof(Site));

        CardinalityOf(graph, typeof(Site), nameof(Site.backup)).Should().Be(Cardinality.Optional);
        CardinalityOf(graph, typeof(Site), nameof(Site.region)).Should().Be(Cardinality.One);
    }

    [Fact]
    public void WithoutNullableAnnotationsNoPredecessorIsOptional()
    {
        var graph = TypeGraph.Discover(typeof(Post));

        CardinalityOf(graph, typeof(Post), nameof(Post.zone)).Should().Be(Cardinality.One);
        CardinalityOf(graph, typeof(Post), nameof(Post.backup)).Should().Be(Cardinality.One);
    }

    [Fact]
    public void AFactTypeReachedThroughTwoRolesIsOneNode()
    {
        var graph = TypeGraph.Discover(typeof(Transfer));

        graph.Nodes.Select(node => node.FactClass).Should().OnlyHaveUniqueItems();
        graph.Edges
            .Where(edge => edge.Successor == typeof(Office) && edge.Predecessor == typeof(Company))
            .Should().ContainSingle();
    }

    [Fact]
    public void FieldsAndTypesThatAreNotFactsAreNotNodes()
    {
        var graph = TypeGraph.Discover(typeof(NotAFact), typeof(string), typeof(Badge));

        graph.Nodes.Select(node => node.FactClass)
            .Should().BeEquivalentTo(new[] { typeof(Badge), typeof(Employee), typeof(Company) });
    }

    [Fact]
    public void CollapsingDeletionMarksTheFactAndRemovesTheMarker()
    {
        var graph = TypeGraph.Discover(typeof(Employee), typeof(EmployeeDeleted)).CollapseDeletion();

        MarkingOf(graph, typeof(Employee)).Should().Be(Marking.Deletable);
        graph.Nodes.Select(node => node.FactClass).Should().NotContain(typeof(EmployeeDeleted));
        graph.Edges.Should().NotContain(edge => edge.Successor == typeof(EmployeeDeleted));
    }

    [Fact]
    public void CollapsingRestorationMarksTheFactAndRemovesBothMarkers()
    {
        var graph = TypeGraph
            .Discover(typeof(Office), typeof(OfficeDeleted), typeof(OfficeRestored))
            .CollapseDeletion();

        MarkingOf(graph, typeof(Office)).Should().Be(Marking.Restorable);
        graph.Nodes.Select(node => node.FactClass)
            .Should().NotContain(new[] { typeof(OfficeDeleted), typeof(OfficeRestored) });
    }

    [Fact]
    public void AMarkerThatRecordsMoreThanTheFactIsNotCollapsed()
    {
        var graph = TypeGraph.Discover(typeof(Badge), typeof(BadgeDeleted)).CollapseDeletion();

        MarkingOf(graph, typeof(Badge)).Should().Be(Marking.None);
        graph.Nodes.Select(node => node.FactClass).Should().Contain(typeof(BadgeDeleted));
    }

    [Fact]
    public void CollapsingAGraphWithoutDeletionsChangesNothing()
    {
        var full = TypeGraph.Discover(TypeCatalog.All);

        var compact = full.CollapseDeletion();

        compact.Nodes.Should().Equal(full.Nodes);
        compact.Edges.Should().Equal(full.Edges);
    }

    [Fact]
    public void TheCompactGraphHoldsNoFactTypeTheFullGraphDoesNot()
    {
        var full = TypeGraph.Discover(TypeCatalog.All.Concat(Fixtures.Deletions).ToArray());

        var compact = full.CollapseDeletion();

        compact.Nodes.Select(node => node.FactClass)
            .Should().BeSubsetOf(full.Nodes.Select(node => node.FactClass));
        compact.Edges.Should().BeSubsetOf(full.Edges);
    }

    [Fact]
    public void CollapsingAnAlreadyCollapsedGraphKeepsItsMarkings()
    {
        var compact = TypeGraph.Discover(typeof(Employee), typeof(EmployeeDeleted)).CollapseDeletion();

        var again = compact.CollapseDeletion();

        MarkingOf(again, typeof(Employee)).Should().Be(Marking.Deletable);
        again.Nodes.Should().Equal(compact.Nodes);
        again.Edges.Should().Equal(compact.Edges);
    }

    private static Cardinality CardinalityOf(TypeGraph graph, Type successor, string role) =>
        graph.Edges.Should()
            .ContainSingle(edge => edge.Successor == successor && edge.Role == role)
            .Subject.Cardinality;

    private static Marking MarkingOf(TypeGraph graph, Type factClass) =>
        graph.Nodes.Should()
            .ContainSingle(node => node.FactClass == factClass)
            .Subject.Marking;
}
