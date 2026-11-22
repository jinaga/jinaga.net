using System.Collections.Immutable;
using System.Linq;
using Jinaga.Pipelines;
using Jinaga.Projections;

namespace Jinaga.Test.Specifications;

/// <summary>
/// The hypotheses of the feeds theorems, each over every descendant of the specification:
/// every descendant is well formed, declares each label once, and has a path condition on
/// every match (<c>JinagaSpec/Feeds.lean</c>). A specification that breaks one has no proven
/// decomposition, so <see cref="FeedBuilder.BuildFeeds"/> refuses it by name rather than
/// returning feeds that silently miss results.
///
/// The feeds themselves are stated by the pinned vectors, which
/// <see cref="Conformance.FeedVectorTest"/> reads. These build specifications directly, as
/// <see cref="WellFormedSpecificationTest"/> does, because the model builder produces
/// specifications that meet the hypotheses by construction.
/// </summary>
public class FeedBuilderTest
{
    private static readonly Role TenantRole = new("tenant", "Tenant");
    private static readonly Role EventRole = new("event", "Event");

    [Fact]
    public void RefusesASpecificationThatIsNotWellFormed()
    {
        // The projection names a label the specification does not declare.
        var specification = Spec(
            new[] { MatchOf("e", "Event", Successor("t", TenantRole)) },
            new SimpleProjection("nosuchlabel", typeof(object)));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().Throw<SpecificationException>().WithMessage(
            "A specification that is not well formed has no feeds. " +
            "The projection names the label 'nosuchlabel', which has not been defined.");
    }

    [Fact]
    public void RefusesAChildSpecificationThatRedeclaresALabelOfItsParent()
    {
        // 'd' is declared inside the parent's negative condition and again by the child. Scope
        // is lexical, so the specification is well formed; joining the levels into one feed
        // would bind 'd' twice.
        var specification = Spec(
            new[]
            {
                MatchOf("e", "Event",
                    new[] { Successor("t", TenantRole) },
                    Exists(false, MatchOf("d", "Delete", Successor("e", EventRole)))),
            },
            Child("d", "Name"));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().Throw<SpecificationException>().WithMessage(
            "Rename the label 'd': feeds need every label to be declared once.");
    }

    [Fact]
    public void RefusesSiblingConditionsThatReuseALabel()
    {
        // Two sibling negative conditions may reuse a name, and a well-formed specification
        // does. Each becomes a level of the same feed, which would then bind 'd' twice.
        var specification = Spec(
            new[]
            {
                MatchOf("e", "Event",
                    new[] { Successor("t", TenantRole) },
                    Exists(false, MatchOf("d", "Delete", Successor("e", EventRole))),
                    Exists(false, MatchOf("d", "Archive", Successor("e", EventRole)))),
            },
            new SimpleProjection("e", typeof(object)));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().Throw<SpecificationException>().WithMessage(
            "Rename the label 'd': feeds need every label to be declared once.");
    }

    [Fact]
    public void RefusesAMatchThatHasNoPathCondition()
    {
        // 'n' is bound to every Name there is, so no feed can deliver what it matches.
        var specification = Spec(
            new[]
            {
                MatchOf("e", "Event", Successor("t", TenantRole)),
                MatchOf("n", "Name"),
            },
            new SimpleProjection("e", typeof(object)));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().Throw<SpecificationException>().WithMessage(
            "The match 'n' has no path condition, so it has no feed: " +
            "every match of a feed is reached from a label.");
    }

    [Fact]
    public void AcceptsSiblingChildSpecificationsThatShareALabel()
    {
        // No feed binds both: a child specification's feeds carry its parent's labels and its
        // own, never a sibling's.
        var specification = Spec(
            new[] { MatchOf("e", "Event", Successor("t", TenantRole)) },
            new CompoundProjection(
                ImmutableDictionary<string, Projection>.Empty
                    .Add("names", Child("c", "Name"))
                    .Add("titles", Child("c", "Title")),
                typeof(object)));

        var feeds = FeedBuilder.BuildFeeds(specification);

        feeds.Select(feed => feed.ToDescriptiveString()).Should().Equal(
            "(t: Tenant) {\n    e: Event [\n        e->tenant: Tenant = t\n    ]\n}\n",
            "(t: Tenant) {\n    e: Event [\n        e->tenant: Tenant = t\n    ]\n    c: Name [\n        c->event: Event = e\n    ]\n}\n",
            "(t: Tenant) {\n    e: Event [\n        e->tenant: Tenant = t\n    ]\n    c: Title [\n        c->event: Event = e\n    ]\n}\n");
    }

    // (t: Tenant) { matches } => projection
    private static Specification Spec(Match[] matches, Projection projection) =>
        new(
            ImmutableList.Create(new SpecificationGiven(
                new Label("t", "Tenant"), ImmutableList<ExistentialCondition>.Empty)),
            matches.ToImmutableList(),
            projection);

    // A child specification of the projection: { label: type [ label->event = e ] } => label
    private static CollectionProjection Child(string label, string type) =>
        new(
            ImmutableList.Create(MatchOf(label, type, Successor("e", EventRole))),
            new SimpleProjection(label, typeof(object)),
            typeof(object));

    private static Match MatchOf(string name, string type, params PathCondition[] paths) =>
        MatchOf(name, type, paths, Array.Empty<ExistentialCondition>());

    private static Match MatchOf(string name, string type, PathCondition[] paths, params ExistentialCondition[] existentials) =>
        new(new Label(name, type), paths.ToImmutableList(), existentials.ToImmutableList());

    /// <summary>A walk from <paramref name="labelRight"/> to the unknown's successors.</summary>
    private static PathCondition Successor(string labelRight, params Role[] rolesLeft) =>
        new(rolesLeft.ToImmutableList(), labelRight, ImmutableList<Role>.Empty);

    private static ExistentialCondition Exists(bool exists, params Match[] matches) =>
        new(exists, matches.ToImmutableList());
}
