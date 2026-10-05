using Jinaga.Notebooks.Test.Models;
using static Jinaga.Notebooks.Dot.Renderer;

namespace Jinaga.Notebooks.Test.TypeGraphs;

public class RenderTypesTest
{
    [Fact]
    public void FactTypeWithoutPredecessorsStandsAlone()
    {
        var graph = RenderedTypeGraph.Full(typeof(Company));

        graph.ShouldShowExactly(typeof(Company));
        graph.ShouldShowNoPredecessorsOf<Company>();
    }

    [Fact]
    public void PredecessorIsLabelledWithItsRole()
    {
        var graph = RenderedTypeGraph.Full(typeof(Office));

        graph.ShouldShowPredecessor<Office, Company>(nameof(Office.company));
    }

    [Fact]
    public void ArrayOfPredecessorsHasManyCardinality()
    {
        var graph = RenderedTypeGraph.Full(typeof(Assignment));

        graph.ShouldShowPredecessor<Assignment, Office>(nameof(Assignment.offices), Cardinality.Many);
        graph.ShouldShowPredecessor<Assignment, Employee>(nameof(Assignment.employee));
    }

    [Fact]
    public void PredecessorsAreFollowedTransitively()
    {
        var graph = RenderedTypeGraph.Full(typeof(Assignment));

        graph.ShouldShowExactly(typeof(Assignment), typeof(Employee), typeof(Office), typeof(Company));
        graph.ShouldShowPredecessor<Office, Company>(nameof(Office.company));
        graph.ShouldShowPredecessor<Employee, Company>(nameof(Employee.company));
    }

    [Fact]
    public void SuccessorsAreShownOnlyWhenRequested()
    {
        var graph = RenderedTypeGraph.Full(typeof(Company));

        graph.ShouldNotShow<Office>();
    }

    [Fact]
    public void SharedPredecessorIsShownOnce()
    {
        var graph = RenderedTypeGraph.Full(typeof(Office), typeof(Employee));

        graph.ShouldShowExactly(typeof(Office), typeof(Employee), typeof(Company));
        graph.ShouldShowNoPredecessorsOf<Company>();
    }

    [Fact]
    public void EachRoleOfTheSameTypeIsAPredecessor()
    {
        var graph = RenderedTypeGraph.Full(typeof(Transfer));

        graph.ShouldShowPredecessor<Transfer, Office>(nameof(Transfer.from));
        graph.ShouldShowPredecessor<Transfer, Office>(nameof(Transfer.to));
    }

    [Fact]
    public void MutablePropertyIsItsOwnPredecessor()
    {
        var graph = RenderedTypeGraph.Full(typeof(OfficeName));

        graph.ShouldShowPredecessor<OfficeName, OfficeName>(nameof(OfficeName.prior), Cardinality.Many);
        graph.ShouldShowPredecessor<OfficeName, Office>(nameof(OfficeName.office));
    }

    [Fact]
    public void FieldsAreNotPredecessors()
    {
        var graph = RenderedTypeGraph.Full(typeof(Badge));

        graph.ShouldShowExactly(typeof(Badge), typeof(Employee), typeof(Company));
    }

    [Fact]
    public void TypesThatAreNotFactsAreIgnored()
    {
        var graph = RenderedTypeGraph.Full(typeof(NotAFact), typeof(string), typeof(Office));

        graph.ShouldShowExactly(typeof(Office), typeof(Company));
    }

    [Fact]
    public void RequestingATypeTwiceShowsItOnce()
    {
        var graph = RenderedTypeGraph.Full(typeof(Office), typeof(Office), typeof(Company));

        graph.ShouldShowSameGraphAs(RenderedTypeGraph.Full(typeof(Office)));
    }

    [Fact]
    public void NoFactTypeIsMarked()
    {
        var graph = RenderedTypeGraph.Full(TypeCatalog.All);

        foreach (var type in TypeCatalog.All.Where(type => type != typeof(NotAFact)))
        {
            graph.ShouldShow(type).Unmarked();
        }
    }

    [Theory]
    [MemberData(nameof(TypeCatalog.Orderings), MemberType = typeof(TypeCatalog))]
    public void OrderOfRequestedTypesDoesNotMatter(Type[] types)
    {
        RenderedTypeGraph.Full(types)
            .ShouldShowSameGraphAs(RenderedTypeGraph.Full(TypeCatalog.All));
    }
}
