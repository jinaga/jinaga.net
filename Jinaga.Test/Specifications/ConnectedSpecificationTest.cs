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
/// names the wrong side of the disconnection fails even though it reads well.
///
/// They assert the <see cref="Disconnection"/>'s kind and its labels, not its prose. A
/// <see cref="Disconnection"/> holds structure alone and renders its message from it, so an
/// assertion that matched a sentence would be a second copy of that sentence: red for every
/// rewording and silent about whether the analysis is right. The prose stands on review.
/// </summary>
public class ConnectedSpecificationTest
{
    [Fact]
    public void NamesBothChainsOfASpecificationThatIsAProduct()
    {
        // Each match reaches its own given, and nothing reaches both.
        var specification = Vector("two-chains-of-predecessors");

        var disconnection = ConnectedSpecification.Disconnect(specification);

        disconnection!.Kind.Should().Be(DisconnectionKind.SeparateChains);
        disconnection.Chains.Should().HaveCount(specification.Matches.Count);
        for (int index = 0; index < specification.Matches.Count; index++)
        {
            var match = specification.Matches[index];
            disconnection.Chains[index].Unknowns.Should().Equal(match.Unknown.Name);
            disconnection.Chains[index].Givens.Should().Equal(Given(match));
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
        disconnection.ChildPath.Should().Be(childName);

        // The child's own chain first, then its parent's, each with the given it reaches.
        disconnection.Chains.Should().HaveCount(2);
        disconnection.Chains[0].Unknowns.Should().Equal(column.Unknown.Name);
        disconnection.Chains[0].Givens.Should().Equal(Given(column));
        disconnection.Chains[1].Unknowns.Should().Equal(parent.Unknown.Name);
        disconnection.Chains[1].Givens.Should().Equal(Given(parent));

        // The match that joins them, and the child that declares it.
        disconnection.JoinerPath.Should().Be($"{childName}.{grandchildName}");
        disconnection.JoiningMatch.Should().BeSameAs(cell);
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
        disconnection.ConditionLabel.Should().Be(carrier.Unknown.Name);

        // The carrier's chain first, then the one across the cut.
        disconnection.Chains.Should().HaveCount(2);
        disconnection.Chains[0].Unknowns.Should().Equal(carrier.Unknown.Name);
        disconnection.Chains[0].Givens.Should().Equal(Given(carrier));
        disconnection.Chains[1].Unknowns.Should().Equal(away.Unknown.Name);
        disconnection.Chains[1].Givens.Should().Equal(Given(away));

        disconnection.JoiningMatch!.Unknown.Name.Should().Be(joiner.Unknown.Name);
    }

    [Fact]
    public void NamesTheConditionsMatchAndTheGivenItReaches()
    {
        var specification = Vector("negative-condition-joins-another-given");
        var carrier = specification.Matches.Single();
        var inner = carrier.ExistentialConditions.Single().Matches.Single();

        var disconnection = ConnectedSpecification.Disconnect(specification);

        disconnection!.Kind.Should().Be(DisconnectionKind.ConditionUnrelated);
        disconnection.ConditionLabel.Should().Be(carrier.Unknown.Name);

        // The condition's own label stands for the near side, so the one chain is the far one.
        disconnection.Chains.Should().HaveCount(1);
        disconnection.Chains[0].Unknowns.Should().Equal(inner.Unknown.Name);
        disconnection.Chains[0].Givens.Should().Equal(Given(inner));
    }

    [Fact]
    public void NamesTheTwoChainsAFilterWouldJoin()
    {
        var specification = Vector("negative-condition-joins-two-chains");
        var carrier = specification.Matches.Single(match => match.ExistentialConditions.Any());
        var away = specification.Matches.Single(match => match != carrier);

        var disconnection = ConnectedSpecification.Disconnect(specification);

        disconnection!.Kind.Should().Be(DisconnectionKind.JoinedOnlyByCondition);
        disconnection.ConditionLabel.Should().Be(carrier.Unknown.Name);

        disconnection.Chains.Should().HaveCount(2);
        disconnection.Chains[0].Unknowns.Should().Equal(carrier.Unknown.Name);
        disconnection.Chains[0].Givens.Should().Equal(Given(carrier));
        disconnection.Chains[1].Unknowns.Should().Equal(away.Unknown.Name);
        disconnection.Chains[1].Givens.Should().Equal(Given(away));
    }

    /// <summary>
    /// A given its matches do not use is no chain of its own, so nothing is refused for one
    /// (jinaga/jinaga-spec#60). <c>unused-given</c> carried the opposite verdict at the
    /// previous pin, which is why it is stated here as well as in the vector test.
    /// </summary>
    [Theory]
    [InlineData("unused-given")]
    [InlineData("no-matches")]
    [InlineData("no-matches-two-givens")]
    public void FindsNoDisconnectionInAGivenNoMatchUses(string name)
    {
        var specification = Vector(name);
        var joined = Joined(specification.Matches);
        specification.Givens.Should().Contain(given => !joined.Contains(given.Label.Name),
            "the vector is one whose givens its matches do not all use");

        ConnectedSpecification.Disconnect(specification).Should().BeNull();
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
