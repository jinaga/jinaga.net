using Jinaga.Notebooks.Test.Models;

namespace Jinaga.Notebooks.Test.TypeGraphs;

public class RenderTypesCompactTest
{
    [Fact]
    public void DeletableFactIsMarkedAndItsDeletionHidden()
    {
        var graph = RenderedTypeGraph.Compact(typeof(Employee), typeof(EmployeeDeleted));

        graph.ShouldShow<Employee>().MarkedDeletable();
        graph.ShouldNotShow<EmployeeDeleted>();
        graph.ShouldShowPredecessor<Employee, Company>(nameof(Employee.company));
    }

    [Fact]
    public void RestorableFactIsMarkedAndItsDeletionAndRestorationHidden()
    {
        var graph = RenderedTypeGraph.Compact(typeof(Office), typeof(OfficeDeleted), typeof(OfficeRestored));

        graph.ShouldShow<Office>().MarkedRestorable();
        graph.ShouldNotShow<OfficeDeleted>();
        graph.ShouldNotShow<OfficeRestored>();
        graph.ShouldShowPredecessor<Office, Company>(nameof(Office.company));
    }

    [Fact]
    public void DeletionIsRecognizedOnlyWhenRequested()
    {
        var graph = RenderedTypeGraph.Compact(typeof(Office));

        graph.ShouldShow<Office>().Unmarked();
    }

    [Fact]
    public void RestorationIsRecognizedOnlyWhenRequested()
    {
        var graph = RenderedTypeGraph.Compact(typeof(Office), typeof(OfficeDeleted));

        graph.ShouldShow<Office>().MarkedDeletable();
    }

    [Fact]
    public void NameEndingInDeletedIsNotADeletionOfAnotherFact()
    {
        var graph = RenderedTypeGraph.Compact(typeof(Company), typeof(AuditDeleted));

        graph.ShouldShow<Company>().Unmarked();
        graph.ShouldShowPredecessor<AuditDeleted, Company>(nameof(AuditDeleted.company));
    }

    [Fact]
    public void DeletionIsNamedWithADot()
    {
        var graph = RenderedTypeGraph.Compact(typeof(Company), typeof(CompanyDeleted));

        graph.ShouldShow<Company>().Unmarked();
        graph.ShouldShowPredecessor<CompanyDeleted, Company>(nameof(CompanyDeleted.company));
    }

    [Fact]
    public void DeletionWithAnotherPredecessorIsShown()
    {
        var graph = RenderedTypeGraph.Compact(typeof(Badge), typeof(BadgeDeleted));

        graph.ShouldShow<Badge>().Unmarked();
        graph.ShouldShowPredecessor<BadgeDeleted, Badge>(nameof(BadgeDeleted.badge));
        graph.ShouldShowPredecessor<BadgeDeleted, Employee>(nameof(BadgeDeleted.by));
    }

    [Fact]
    public void DeletionWithOtherSuccessorsIsShown()
    {
        var graph = RenderedTypeGraph.Compact(
            typeof(Assignment), typeof(AssignmentDeleted), typeof(AssignmentDeletionReason));

        graph.ShouldShow<Assignment>().Unmarked();
        graph.ShouldShowPredecessor<AssignmentDeleted, Assignment>(nameof(AssignmentDeleted.assignment));
        graph.ShouldShowPredecessor<AssignmentDeletionReason, AssignmentDeleted>(nameof(AssignmentDeletionReason.deleted));
    }

    [Fact]
    public void RestorationWithAnotherPredecessorIsShown()
    {
        var graph = RenderedTypeGraph.Compact(typeof(Transfer), typeof(TransferDeleted), typeof(TransferRestored));

        graph.ShouldShow<Transfer>().Unmarked();
        graph.ShouldShowPredecessor<TransferDeleted, Transfer>(nameof(TransferDeleted.transfer));
        graph.ShouldShowPredecessor<TransferRestored, TransferDeleted>(nameof(TransferRestored.deleted));
        graph.ShouldShowPredecessor<TransferRestored, Employee>(nameof(TransferRestored.by));
    }

    [Fact]
    public void WithoutDeletionsCompactIsTheFullGraph()
    {
        RenderedTypeGraph.Compact(TypeCatalog.All)
            .ShouldShowSameGraphAs(RenderedTypeGraph.Full(TypeCatalog.All));
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void CompactShowsOnlyWhatTheFullGraphShows(Type[] types)
    {
        RenderedTypeGraph.Compact(types).ShouldBeAViewOf(RenderedTypeGraph.Full(types));
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void OrderOfRequestedTypesDoesNotMatter(Type[] types)
    {
        RenderedTypeGraph.Compact(Enumerable.Reverse(types).ToArray())
            .ShouldShowSameGraphAs(RenderedTypeGraph.Compact(types));
    }

    public static IEnumerable<object[]> Scenarios()
    {
        var deletions = new[]
        {
            typeof(EmployeeDeleted), typeof(OfficeDeleted), typeof(OfficeRestored),
            typeof(AuditDeleted), typeof(CompanyDeleted), typeof(BadgeDeleted),
            typeof(AssignmentDeleted), typeof(AssignmentDeletionReason),
            typeof(TransferDeleted), typeof(TransferRestored)
        };
        yield return new object[] { TypeCatalog.All };
        yield return new object[] { deletions };
        yield return new object[] { TypeCatalog.All.Concat(deletions).ToArray() };
    }
}
