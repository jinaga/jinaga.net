using Jinaga.Notebooks.Dot;
using Jinaga.Notebooks.Test.Models;

namespace Jinaga.Notebooks.Test.TypeGraphs;

/// <summary>
/// How the writer spells a graph it is given. Statement order is unspecified, so
/// the whole-document approvals here are of graphs small enough that the walk
/// fixes their order; everything else is asserted as the one line it is about.
/// </summary>
public class DotWriterTest
{
    [Fact]
    public void AFactTypeWithoutPredecessorsIsWrittenAsANode()
    {
        var dot = TypeGraph.Discover(typeof(Company)).ToDot();

        dot.Should().Be(Document(
            "    \"Corporate.Company\""));
    }

    [Fact]
    public void APredecessorIsWrittenAsALabelledEdge()
    {
        var dot = TypeGraph.Discover(typeof(Office)).ToDot();

        dot.Should().Be(Document(
            "    \"Corporate.Office\" -> \"Corporate.Company\" [label=\" company\"]",
            "    \"Corporate.Company\""));
    }

    [Fact]
    public void AMarkedFactTypeIsWrittenAsAFilledNode()
    {
        var dot = TypeGraph.Discover(typeof(Employee), typeof(EmployeeDeleted))
            .CollapseDeletion()
            .ToDot();

        dot.Should().Be(Document(
            "    \"Corporate.Employee\" [style=filled, fillcolor=orange]",
            "    \"Corporate.Employee\" -> \"Corporate.Company\" [label=\" company\"]",
            "    \"Corporate.Company\""));
    }

    [Fact]
    public void AQuoteInAFactTypeNameIsEscaped()
    {
        var dot = TypeGraph.Discover(typeof(Quoted)).ToDot();

        dot.Should().Be(Document(
            "    \"Awkward \\\"Quoted\\\" Name\""));
    }

    [Fact]
    public void AnOptionalPredecessorIsWrittenWithAQuestionMark()
    {
        var dot = TypeGraph.Discover(typeof(Site)).ToDot();

        dot.Should().Contain("\"Optional.Site\" -> \"Optional.Region\" [label=\" backup?\"]");
        dot.Should().Contain("\"Optional.Site\" -> \"Optional.Region\" [label=\" region\"]");
    }

    [Fact]
    public void AnArrayOfPredecessorsIsWrittenWithAnAsterisk()
    {
        var dot = TypeGraph.Discover(typeof(Assignment)).ToDot();

        dot.Should().Contain("\"Corporate.Assignment\" -> \"Corporate.Office\" [label=\" offices*\"]");
    }

    [Fact]
    public void ARestorableFactTypeIsWrittenWithItsOwnColor()
    {
        var dot = TypeGraph.Discover(typeof(Office), typeof(OfficeDeleted), typeof(OfficeRestored))
            .CollapseDeletion()
            .ToDot();

        dot.Should().Contain("\"Corporate.Office\" [style=filled, fillcolor=greenyellow]");
    }

    private static string Document(params string[] statements) =>
        string.Join("\n", new[] { "digraph {", "    rankdir=BT" }.Concat(statements).Append("}"));
}
