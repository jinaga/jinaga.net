using System.Collections.Immutable;
using System.Linq;
using Jinaga.Projections;
using Jinaga.Test.Conformance;

namespace Jinaga.Test.Specifications;

/// <summary>
/// Which advice fits which disconnected shape. <see cref="Conformance.WellFormedVectorTest"/>
/// holds the check to the oracle's verdict; these state the design's claim about what the
/// refusal then tells the developer, and they fail when the analysis picks the wrong chain or
/// the wrong kind.
///
/// Each test reads one of the vectors jinaga-spec generates for the shape, and derives the
/// labels it expects from that specification rather than from the sentence, so a message that
/// names the wrong side of the disconnection fails even though it reads well. The prose itself
/// is not asserted: an assertion that matched a sentence would be a second copy of it, red for
/// every rewording and silent about whether the analysis is right.
/// </summary>
public class ConnectedSpecificationTest
{
    [Fact]
    public void NamesAGivenNoMatchUses()
    {
        var specification = Vector("unused-given");
        var joined = Joined(specification.Matches);
        var unused = specification.Givens
            .Single(given => !joined.Contains(given.Label.Name))
            .Label.Name;

        var disconnection = ConnectedSpecification.Disconnect(specification);

        disconnection!.Kind.Should().Be(DisconnectionKind.UnusedGiven);
        disconnection.Message.Should().Contain($"'{unused}'");
    }

    [Fact]
    public void NamesBothChainsOfASpecificationThatIsAProduct()
    {
        // Each match reaches its own given, and nothing reaches both.
        var specification = Vector("two-chains-of-predecessors");

        var disconnection = ConnectedSpecification.Disconnect(specification);

        disconnection!.Kind.Should().Be(DisconnectionKind.SeparateChains);
        foreach (var match in specification.Matches)
        {
            disconnection.Message.Should().Contain($"'{match.Unknown.Name}'");
            disconnection.Message.Should().Contain(match.PathConditions.Single().LabelRight);
        }
    }

    [Fact]
    public void NamesTheChildTheGrandchildAndTheMatchThatJoinsThem()
    {
        var specification = Vector("child-joins-two-chains-only-in-a-grandchild");
        var parent = specification.Matches.Single();
        var (childName, child) = OnlyChildOf(specification.Projection);
        var column = child.Matches.Single();
        var (grandchildName, grandchild) = OnlyChildOf(child.Projection);
        var cell = grandchild.Matches.Single();

        var disconnection = ConnectedSpecification.Disconnect(specification);

        disconnection!.Kind.Should().Be(DisconnectionKind.ChildJoinsLate);
        disconnection.Message.Should()
            // The child's own chain, and the given it reaches.
            .Contain($"'{childName}'").And.Contain($"'{column.Unknown.Name}'")
            .And.Contain(column.PathConditions.Single().LabelRight)
            // Its parent's chain, and the given that one reaches.
            .And.Contain($"'{parent.Unknown.Name}'")
            .And.Contain(parent.PathConditions.Single().LabelRight)
            // The match that joins them, and the child that declares it.
            .And.Contain($"'{childName}.{grandchildName}'").And.Contain($"'{cell.Unknown.Name}'");
    }

    [Fact]
    public void NamesTheConditionTheChainsAndTheLaterJoin()
    {
        var specification = Vector("negative-condition-before-the-join");
        var carrier = specification.Matches.Single(match => match.ExistentialConditions.Any());
        var joiner = specification.Matches.Single(match =>
            match.PathConditions.Count > 1 && match != carrier);
        var away = specification.Matches.Single(match => match != carrier && match != joiner);

        var disconnection = ConnectedSpecification.Disconnect(specification);

        disconnection!.Kind.Should().Be(DisconnectionKind.ConditionBeforeJoin);
        disconnection.Message.Should()
            .Contain($"'{carrier.Unknown.Name}'").And.Contain(Given(carrier))
            .And.Contain($"'{away.Unknown.Name}'").And.Contain(Given(away))
            .And.Contain($"'{joiner.Unknown.Name}'");
    }

    [Fact]
    public void NamesTheConditionsMatchAndTheGivenItReaches()
    {
        var specification = Vector("negative-condition-joins-another-given");
        var carrier = specification.Matches.Single();
        var inner = carrier.ExistentialConditions.Single().Matches.Single();

        var disconnection = ConnectedSpecification.Disconnect(specification);

        disconnection!.Kind.Should().Be(DisconnectionKind.ConditionUnrelated);
        disconnection.Message.Should()
            .Contain($"'{carrier.Unknown.Name}'")
            .And.Contain($"'{inner.Unknown.Name}'")
            .And.Contain(Given(inner));
    }

    [Fact]
    public void NamesTheTwoChainsAFilterWouldJoin()
    {
        var specification = Vector("negative-condition-joins-two-chains");
        var carrier = specification.Matches.Single(match => match.ExistentialConditions.Any());
        var away = specification.Matches.Single(match => match != carrier);

        var disconnection = ConnectedSpecification.Disconnect(specification);

        disconnection!.Kind.Should().Be(DisconnectionKind.JoinedOnlyByCondition);
        disconnection.Message.Should()
            .Contain($"'{carrier.Unknown.Name}'").And.Contain(Given(carrier))
            .And.Contain($"'{away.Unknown.Name}'").And.Contain(Given(away));
    }

    private static Specification Vector(string name) =>
        ConformanceVector.Load("well-formed", name).Specification;

    /// <summary>The labels the matches join to, at this level.</summary>
    private static ImmutableList<string> Joined(ImmutableList<Match> matches) =>
        matches.SelectMany(match => match.PathConditions)
            .Select(condition => condition.LabelRight)
            .ToImmutableList();

    /// <summary>The one given the match's own path conditions reach.</summary>
    private static string Given(Match match) => Joined(ImmutableList.Create(match)).Distinct().Single();

    /// <summary>The one child specification a composite projection holds, and its name.</summary>
    private static (string Name, CollectionProjection Child) OnlyChildOf(Projection projection)
    {
        var compound = (CompoundProjection)projection;
        var name = compound.Names.Single();
        return (name, (CollectionProjection)compound.GetProjection(name));
    }
}
