using Jinaga.Notebooks.Test.Models;

namespace Jinaga.Notebooks.Test.FactGraphs;

public class RenderFactsTest
{
    private readonly JinagaClient j = JinagaTest.Create();

    private static readonly Company acme = new("Acme");
    private static readonly Office dallas = new(acme, "Dallas");
    private static readonly Office austin = new(acme, "Austin");
    private static readonly Employee alice = new(acme, 1001);

    [Fact]
    public void RequestedFactIsShownHighlightedWithItsType()
    {
        var graph = RenderedFactGraph.Of(j, acme);

        graph.ShouldShowExactly(acme);
        graph.ShouldShow(acme).OfItsType().Highlighted();
    }

    [Fact]
    public void PredecessorsAreShownButNotHighlighted()
    {
        var graph = RenderedFactGraph.Of(j, dallas);

        graph.ShouldShowExactly(dallas, acme);
        graph.ShouldShow(acme).OfItsType().NotHighlighted();
        graph.ShouldShowPredecessor(dallas, nameof(Office.company), acme);
    }

    [Fact]
    public void RequestedPredecessorIsHighlighted()
    {
        var graph = RenderedFactGraph.Of(j, dallas, acme);

        graph.ShouldShow(dallas).Highlighted();
        graph.ShouldShow(acme).Highlighted();
    }

    [Fact]
    public void EachFactInAnArrayOfPredecessorsIsAPredecessor()
    {
        var assignment = new Assignment(alice, new[] { dallas, austin });

        var graph = RenderedFactGraph.Of(j, assignment);

        graph.ShouldShowPredecessor(assignment, nameof(Assignment.offices), dallas);
        graph.ShouldShowPredecessor(assignment, nameof(Assignment.offices), austin);
        graph.ShouldShowPredecessor(assignment, nameof(Assignment.employee), alice);
        graph.ShouldShowPredecessorCount(assignment, 3);
    }

    [Fact]
    public void EachRoleOfTheSameTypeIsAPredecessor()
    {
        var transfer = new Transfer(alice, dallas, austin);

        var graph = RenderedFactGraph.Of(j, transfer);

        graph.ShouldShowPredecessor(transfer, nameof(Transfer.from), dallas);
        graph.ShouldShowPredecessor(transfer, nameof(Transfer.to), austin);
    }

    [Fact]
    public void SharedPredecessorIsShownOnce()
    {
        var graph = RenderedFactGraph.Of(j, dallas, austin);

        graph.ShouldShowExactly(dallas, austin, acme);
    }

    [Fact]
    public void FieldsAreShownInOrderWithTheirValues()
    {
        var badge = new Badge(alice, "B-17", 3, true, null);

        var graph = RenderedFactGraph.Of(j, badge);

        graph.ShouldShow(badge)
            .WithFields(nameof(Badge.code), nameof(Badge.clearance), nameof(Badge.active), nameof(Badge.note))
            .WithField(nameof(Badge.code), "B-17")
            .WithField(nameof(Badge.clearance), "3")
            .WithField(nameof(Badge.active), "true");
    }

    [Fact]
    public void NullStringIsShownAsNull()
    {
        var badge = new Badge(alice, "B-17", 3, true, null);

        var graph = RenderedFactGraph.Of(j, badge);

        graph.ShouldShow(badge).WithField(nameof(Badge.note), "null");
    }

    [Fact]
    public void LongStringIsShortenedToTwentyCharacters()
    {
        var office = new Office(acme, "Llanfairpwllgwyngyllgogerychwyrndrobwllllantysiliogogogoch");

        var graph = RenderedFactGraph.Of(j, office);

        graph.ShouldShow(office).WithField(nameof(Office.city), "Llanfairpwllgwyngyll...");
    }

    [Fact]
    public void MarkupInAStringIsShownAsText()
    {
        var office = new Office(acme, "<b>R&D</b>");

        var graph = RenderedFactGraph.Of(j, office);

        graph.ShouldShow(office).WithField(nameof(Office.city), "<b>R&D</b>");
    }

    [Fact]
    public void FactsAreFoundWithinProjections()
    {
        var projection = new
        {
            headquarters = dallas,
            staff = new[] { new { employee = alice } }
        };

        var graph = RenderedFactGraph.Of(j, projection);

        graph.ShouldShowExactly(dallas, alice, acme);
        graph.ShouldShow(dallas).Highlighted();
        graph.ShouldShow(alice).Highlighted();
    }

    [Fact]
    public void FactsNestedBeyondFiveLevelsAreNotFound()
    {
        var fiveDeep = new { a = new { b = new { c = new { d = new { e = acme } } } } };
        var sixDeep = new { f = fiveDeep };

        RenderedFactGraph.Of(j, fiveDeep).ShouldShowExactly(acme);
        RenderedFactGraph.Of(j, sixDeep).ShouldShowNothing();
    }

    [Fact]
    public void NullsAndStringsInProjectionsAreIgnored()
    {
        var graph = RenderedFactGraph.Of(j, new { name = "Acme", missing = (Company?)null, company = acme });

        graph.ShouldShowExactly(acme);
    }
}
