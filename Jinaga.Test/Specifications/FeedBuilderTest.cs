using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Test.Conformance;

namespace Jinaga.Test.Specifications;

/// <summary>
/// The hypotheses of the feeds theorems, each over every descendant of the specification:
/// every descendant is well formed, declares each label once, has a path condition on every
/// match, and is connected, so that no feed is a product of two chains nothing joins
/// (<c>JinagaSpec/Feeds.lean</c>, <c>JinagaSpec/Connected.lean</c>). A specification that
/// breaks one has no proven decomposition, so <see cref="FeedBuilder.BuildFeeds"/> refuses it
/// by name rather than returning feeds that silently miss results, or that a store can only
/// answer as a cross product.
///
/// The feeds themselves are stated by the pinned vectors, which
/// <see cref="Conformance.FeedVectorTest"/> reads. These build specifications directly, as
/// <see cref="WellFormedSpecificationTest"/> does, because the model builder produces
/// specifications that meet the hypotheses by construction.
///
/// The rewrites below are the ones the connectivity refusals advise. Each one is built here
/// and accepted, which is what keeps the advice true: a rewrite the library would refuse is a
/// message that sends the developer in a circle.
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

        // The rule and the remedy: the match, and the label already in scope that it could be
        // joined to. The prose stands on review; what this pins is that both are named, so a
        // developer reading the refusal knows which match and what to do about it.
        building.Should().Throw<SpecificationException>().Which.Message.Should()
            .Contain("'n'").And.Contain("'e'").And.Contain("n->e = e");
    }

    [Fact]
    public void RefusesAGivenThatCarriesAnExistentialCondition()
    {
        // jinaga-spec states the feeds over a specification whose givens are labels alone, so
        // no theorem says what the feeds of this one are. A store reads nothing at all while a
        // condition on a given fails, so a feed that had quietly dropped the condition would
        // deliver facts the specification excludes.
        var specification = new Specification(
            ImmutableList.Create(new SpecificationGiven(
                new Label("t", "Tenant"),
                ImmutableList.Create(Exists(true, MatchOf("a", "Active", Successor("t", TenantRole)))))),
            ImmutableList.Create(MatchOf("e", "Event", Successor("t", TenantRole))),
            new SimpleProjection("e", typeof(object)));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().Throw<SpecificationException>().WithMessage(
            "The given 't' carries an existential condition, which a feed cannot: " +
            "the feeds are defined over a specification whose givens are labels alone.");
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

    /// <summary>
    /// The well-formed vectors the oracle calls disconnected, selected by their expected
    /// fields so that a re-pin which adds one brings it here with no edit of this list.
    /// </summary>
    public static IEnumerable<object[]> Disconnected =>
        ConformanceVector.Names("well-formed")
            .Select(row => (string)row[0])
            .Where(name =>
            {
                var expected = ConformanceVector.Load("well-formed", name).Expected;
                return expected.GetProperty("wellFormed").GetBoolean() &&
                    !expected.GetProperty("connected").GetBoolean();
            })
            .Select(name => new object[] { name });

    /// <summary>
    /// Every specification the oracle calls a product is refused by name. The refusal is the
    /// connectivity check's own message, except where a refusal above it reaches the same
    /// specification first: the connectivity check assumes every match is anchored, so a
    /// match with no path condition is refused there instead.
    /// </summary>
    [Theory]
    [MemberData(nameof(Disconnected))]
    public void RefusesASpecificationWhoseFeedsWouldBeProducts(string name)
    {
        var specification = ConformanceVector.Load("well-formed", name).Specification;

        var building = () => FeedBuilder.BuildFeeds(specification);

        var message = building.Should().Throw<SpecificationException>().Which.Message;
        var rootless = Rootless(specification);
        if (rootless == null)
        {
            message.Should().Be(
                ConnectedSpecification.Disconnect(specification)!.Message,
                "the connectivity check is what refuses it");
        }
        else
        {
            message.Should().Contain($"'{rootless.Unknown.Name}'").And.Contain("no path condition");
        }
    }

    [Fact]
    public void NamesEveryChainOfASpecificationThatFallsIntoMoreThanTwo()
    {
        // No vector covers three chains: the generator writes the shape with two. The refusal
        // still has to name every group, because a developer who removed only the two it
        // mentioned would be refused again.
        var specification = Rewrite(
            new[] { GivenOf("p1", "R"), GivenOf("p2", "A"), GivenOf("p3", "E") },
            new[]
            {
                MatchOf("a", "A", Down("r", "R", "p1")),
                MatchOf("b", "B", Down("a", "A", "p2")),
                MatchOf("c", "C", Down("e", "E", "p3")),
            },
            Composite(("a", Fact("a")), ("b", Fact("b")), ("c", Fact("c"))));

        var building = () => FeedBuilder.BuildFeeds(specification);

        var message = building.Should().Throw<SpecificationException>().Which.Message;
        foreach (var match in specification.Matches)
        {
            message.Should().Contain($"'{match.Unknown.Name}'");
        }
        foreach (var given in specification.Givens)
        {
            message.Should().Contain(given.Label.Name);
        }
    }

    [Fact]
    public void AdvisesAGivenWhereNothingIsInScopeForAMatchToJoin()
    {
        // A first match of a specification with no givens. Nothing is matched around it, so the
        // refusal has no label to offer, and must not name one: a rewrite that joined 'n' to a
        // label the specification does not declare would send the developer nowhere.
        var specification = Rewrite(
            Array.Empty<SpecificationGiven>(),
            new[] { MatchOf("n", "Name") },
            Fact("n"));

        var building = () => FeedBuilder.BuildFeeds(specification);

        var message = building.Should().Throw<SpecificationException>().Which.Message;
        message.Should().Contain("'n'").And.NotContain("n->", "there is no label to reach");
    }

    /* ---------------------------- The rewrites it advises ---------------------------- */

    [Fact]
    public void AcceptsTwoChainsAsSiblingChildren()
    {
        // (p1: Post, p2: Post) { } => { a = { a: Author [a = p1->author: Author] } => a,
        //                               b = { b: Author [b = p2->author: Author] } => b }
        var specification = Rewrite(
            new[] { GivenOf("p1", "Post"), GivenOf("p2", "Post") },
            Array.Empty<Match>(),
            Composite(
                ("a", Collection(new[] { MatchOf("a", "Author", Up("p1", "author", "Author")) }, Fact("a"))),
                ("b", Collection(new[] { MatchOf("b", "Author", Up("p2", "author", "Author")) }, Fact("b")))));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().NotThrow();
    }

    [Fact]
    public void AcceptsAMatrixDeclaredInTwoLevels()
    {
        // The cells that exist, with each column reached from its cell.
        // (p1: R, p2: A) { u: A [u->a: A = p2] } =>
        //   { k = { x: B [x->u: A = u]  c: A [c = x->c: A  c->r: R = p1] } => { c = c, x = x } }
        var specification = Over(
            new[] { MatchOf("u", "A", Down("a", "A", "p2")) },
            Composite(("k", Collection(
                new[]
                {
                    MatchOf("x", "B", Down("u", "A", "u")),
                    MatchOf("c", "A", Up("x", "c", "A"), Down("r", "R", "p1")),
                },
                Composite(("c", Fact("c")), ("x", Fact("x")))))));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().NotThrow();
    }

    [Fact]
    public void AcceptsADenseMatrixAsThreeSiblingChildren()
    {
        // Every column, even those with no cells: the rows, the columns and the cells each in
        // their own child, with no matches above them.
        var specification = Over(
            Array.Empty<Match>(),
            Composite(
                ("rows", Collection(new[] { MatchOf("u", "A", Down("a", "A", "p2")) }, Fact("u"))),
                ("columns", Collection(new[] { MatchOf("c", "A", Down("r", "R", "p1")) }, Fact("c"))),
                ("cells", Collection(
                    new[]
                    {
                        MatchOf("u", "A", Down("a", "A", "p2")),
                        MatchOf("x", "B", Down("u", "A", "u")),
                        MatchOf("c", "A", Up("x", "c", "A"), Down("r", "R", "p1")),
                    },
                    Fact("x")))));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().NotThrow();
    }

    [Fact]
    public void AcceptsAConditionReorderedToStartFromTheFilteredMatch()
    {
        // (p1: R, p2: A) { c: A [c->r: R = p1  !E { e: E [e->c: A = c] }]  x: B [x->c: A = c]
        //                  u: A [u = x->u: A  u->a: A = p2] } => x
        var specification = Over(
            new[]
            {
                MatchOf("c", "A", new[] { Down("r", "R", "p1") },
                    Exists(false, MatchOf("e", "E", Down("c", "A", "c")))),
                MatchOf("x", "B", Down("c", "A", "c")),
                MatchOf("u", "A", Up("x", "u", "A"), Down("a", "A", "p2")),
            },
            Fact("x"));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().NotThrow();
    }

    [Fact]
    public void AcceptsAConditionReorderedToFollowTheJoin()
    {
        // (p1: R, p2: A) { u: A [u->a: A = p2]  x: B [x->u: A = u]
        //                  c: A [c = x->c: A  c->r: R = p1  !E { e: E [e->c: A = c] }] } => x
        var specification = Over(
            new[]
            {
                MatchOf("u", "A", Down("a", "A", "p2")),
                MatchOf("x", "B", Down("u", "A", "u")),
                MatchOf("c", "A", new[] { Up("x", "c", "A"), Down("r", "R", "p1") },
                    Exists(false, MatchOf("e", "E", Down("c", "A", "c")))),
            },
            Fact("x"));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().NotThrow();
    }

    [Fact]
    public void AcceptsAConditionJoinedToTheMatchItFilters()
    {
        // (p1: R, p2: A) { u: A [u->a: A = p2  !E { e: E [e->u: A = u  e->r: R = p1] }] } => u
        var specification = Over(
            new[]
            {
                MatchOf("u", "A", new[] { Down("a", "A", "p2") },
                    Exists(false, MatchOf("e", "E", Down("u", "A", "u"), Down("r", "R", "p1")))),
            },
            Fact("u"));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().NotThrow();
    }

    [Fact]
    public void AcceptsAGateOnAGivenReadAsASiblingChild()
    {
        // (p1: R, p2: A) { } => { us = { u: A [u->a: A = p2] } => u,
        //                         es = { e: E [e->r: R = p1] } => e }
        var specification = Over(
            Array.Empty<Match>(),
            Composite(
                ("us", Collection(new[] { MatchOf("u", "A", Down("a", "A", "p2")) }, Fact("u"))),
                ("es", Collection(new[] { MatchOf("e", "E", Down("r", "R", "p1")) }, Fact("e")))));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().NotThrow();
    }

    [Fact]
    public void AcceptsAChainMovedInsideTheConditionThatFiltersIt()
    {
        // (p1: R, p2: A) { x: X [x->a: A = p2
        //                  !E { u: U [u->r: R = p1]  e: E [e->u: U = u  e->x: X = x] }] } => x
        var specification = Over(
            new[]
            {
                MatchOf("x", "X", new[] { Down("a", "A", "p2") },
                    Exists(false,
                        MatchOf("u", "U", Down("r", "R", "p1")),
                        MatchOf("e", "E", Down("u", "U", "u"), Down("x", "X", "x")))),
            },
            Fact("x"));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().NotThrow();
    }

    [Fact]
    public void AcceptsADenseComplementReadAsSiblingChildren()
    {
        // (p1: R, p2: A) { } => { links = { u: U [u->r: R = p1]  e: E [e->u: U = u]
        //                                   x: X [x = e->x: X  x->a: A = p2] } => { u = u, x = x },
        //                         xs = { x: X [x->a: A = p2] } => x }
        var specification = Over(
            Array.Empty<Match>(),
            Composite(
                ("links", Collection(
                    new[]
                    {
                        MatchOf("u", "U", Down("r", "R", "p1")),
                        MatchOf("e", "E", Down("u", "U", "u")),
                        MatchOf("x", "X", Up("e", "x", "X"), Down("a", "A", "p2")),
                    },
                    Composite(("u", Fact("u")), ("x", Fact("x"))))),
                ("xs", Collection(new[] { MatchOf("x", "X", Down("a", "A", "p2")) }, Fact("x")))));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().NotThrow();
    }

    [Fact]
    public void AcceptsAnUnanchoredMatchGivenAPathCondition()
    {
        // The rewrite the reworded rootedness refusal advises: 'e' joined to 'u'.
        // (p: P) { u: U [u = p->up: U
        //                !E { e: E [e->u: U = u  !E { f: F [f->e: E = e  f->u: U = u] }] }]
        //          x: X [x->u: U = u] } => x
        var specification = Rewrite(
            new[] { GivenOf("p", "P") },
            new[]
            {
                MatchOf("u", "U", new[] { Up("p", "up", "U") },
                    Exists(false, MatchOf("e", "E", new[] { Down("u", "U", "u") },
                        Exists(false, MatchOf("f", "F", Down("e", "E", "e"), Down("u", "U", "u")))))),
                MatchOf("x", "X", Down("u", "U", "u")),
            },
            Fact("x"));

        var building = () => FeedBuilder.BuildFeeds(specification);

        building.Should().NotThrow();
    }

    /// <summary>
    /// A given its matches do not use is allowed (jinaga/jinaga-spec#60): a feed takes only
    /// the givens it uses, and no match reads the others, so there is no chain for one to be
    /// on. The three vectors are the whole of the shape: a given unused beside one that is
    /// used, a specification with no matches at all, and one with no matches and a given it
    /// does not even project.
    ///
    /// They are read from the pinned vectors rather than built here, because the oracle's
    /// verdict on each is what makes them the cases: the previous pin refused
    /// <c>unused-given</c>, and this test is what fails if the clause comes back.
    /// </summary>
    [Theory]
    [InlineData("unused-given")]
    [InlineData("no-matches")]
    [InlineData("no-matches-two-givens")]
    public void AcceptsAGivenThatItsMatchesDoNotUse(string name)
    {
        var vector = ConformanceVector.Load("well-formed", name);
        vector.Expected.GetProperty("connected").GetBoolean().Should().BeTrue(
            "the oracle allows an unused given");

        var building = () => FeedBuilder.BuildFeeds(vector.Specification);

        building.Should().NotThrow();
    }

    /* ----------------------------------- Builders ----------------------------------- */

    // The first match that no path condition anchors, at any depth and in any child
    // specification, or null where every match is anchored.
    private static Match? Rootless(Specification specification) =>
        Rootless(specification.Matches) ??
        ChildMatches(specification.Projection).Select(Rootless).FirstOrDefault(found => found != null);

    private static Match? Rootless(ImmutableList<Match> matches)
    {
        foreach (var match in matches)
        {
            if (match.PathConditions.IsEmpty)
            {
                return match;
            }
            var inner = match.ExistentialConditions
                .Select(condition => Rootless(condition.Matches))
                .FirstOrDefault(found => found != null);
            if (inner != null)
            {
                return inner;
            }
        }
        return null;
    }

    private static IEnumerable<ImmutableList<Match>> ChildMatches(Projection projection)
    {
        switch (projection)
        {
            case CollectionProjection collection:
                return new[] { collection.Matches }.Concat(ChildMatches(collection.Projection));
            case CompoundProjection compound:
                return compound.Names.SelectMany(name => ChildMatches(compound.GetProjection(name)));
            default:
                return Enumerable.Empty<ImmutableList<Match>>();
        }
    }

    // (p1: R, p2: A) { matches } => projection, the givens the rewrites above are written over.
    private static Specification Over(Match[] matches, Projection projection) =>
        Rewrite(new[] { GivenOf("p1", "R"), GivenOf("p2", "A") }, matches, projection);

    private static Specification Rewrite(
        SpecificationGiven[] givens, Match[] matches, Projection projection) =>
        new(givens.ToImmutableList(), matches.ToImmutableList(), projection);

    private static SpecificationGiven GivenOf(string name, string type) =>
        new(new Label(name, type), ImmutableList<ExistentialCondition>.Empty);

    /// <summary>A walk to the unknown's successors: <c>unknown->role: type = label</c>.</summary>
    private static PathCondition Down(string role, string type, string label) =>
        new(ImmutableList.Create(new Role(role, type)), label, ImmutableList<Role>.Empty);

    /// <summary>A walk to a predecessor of the label: <c>unknown = label->role: type</c>.</summary>
    private static PathCondition Up(string label, string role, string type) =>
        new(ImmutableList<Role>.Empty, label, ImmutableList.Create(new Role(role, type)));

    private static CompoundProjection Composite(params (string Name, Projection Projection)[] components) =>
        new(components.ToImmutableDictionary(
            component => component.Name, component => component.Projection), typeof(object));

    private static CollectionProjection Collection(Match[] matches, Projection projection) =>
        new(matches.ToImmutableList(), projection, typeof(object));

    private static SimpleProjection Fact(string label) => new(label, typeof(object));

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
