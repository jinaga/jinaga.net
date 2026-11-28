using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Jinaga.Projections;

namespace Jinaga.Test.Conformance;

/// <summary>
/// The reference vectors of jinaga.js: the feeds its own <c>buildFeeds</c> makes of a
/// specification, and the skeleton it gives each of them.
///
/// <para>
/// A replicator decides whether to serve a feed by comparing its skeleton with the skeletons
/// of the feeds of its distribution rules, so a port that does not reproduce jinaga.js's
/// skeletons makes distribution decisions the replicator does not make. jinaga-spec has
/// nothing to say here: it does not define skeletons, so jinaga.js is the reference, and its
/// output reaches this repository as the pinned vectors rather than as strings copied into a
/// test.
/// </para>
///
/// <para>
/// The feeds are a different matter. This library builds the feeds jinaga-spec proves, not
/// the ones jinaga.js builds, and the two are known to differ
/// (<see cref="FeedBuilder"/>, and the decisions recorded on jinaga/jinaga.net#201 and
/// jinaga/jinaga.net#394). So
/// <see cref="TheFeedsEitherAgreeWithJinagaJsOrAreAKnownDifference"/> states which vectors
/// agree and which do not. jinaga/jinaga.js#463 is jinaga.js converging on the spec, and it
/// is what empties the list below; nothing here is patched to make the two agree.
/// </para>
/// </summary>
public class SkeletonVectorTest
{
    public static IEnumerable<object[]> Vectors => ReferenceVector.Names("feeds");

    /// <summary>
    /// The skeleton of every feed the reference states, built by this port from the feed
    /// specification the reference states. This is the criterion the replicator's decision
    /// rests on, and it is independent of whether this library's own feed decomposition
    /// agrees with jinaga.js's: the feed comes from the vector, so only the skeleton is under
    /// test.
    ///
    /// <para>
    /// Compared field by field, through <see cref="SkeletonFields"/>, because
    /// <c>Skeleton</c> has no <c>Equals</c> of its own: the comparisons that decide
    /// distribution belong to the engine, not to the skeleton. Comparing each list on its own
    /// also names which part of the walk a re-pin has moved, rather than reporting one
    /// skeleton unequal to another.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Vectors))]
    public void ThePortBuildsTheSkeletonOfEveryReferenceFeed(string name)
    {
        var vector = ReferenceVector.Load("feeds", name);

        foreach (var feed in vector.Feeds)
        {
            var built = Skeleton.Of(feed.Specification);
            var stated = feed.Skeleton;
            var of = "of the feed {0} of vector '{1}'";

            SkeletonFields.Facts(built).Should()
                .Equal(SkeletonFields.Facts(stated), "the facts " + of, feed.Text, name);
            SkeletonFields.Inputs(built).Should()
                .Equal(SkeletonFields.Inputs(stated), "the inputs " + of, feed.Text, name);
            SkeletonFields.Edges(built).Should()
                .Equal(SkeletonFields.Edges(stated), "the edges " + of, feed.Text, name);
            SkeletonFields.ConditionPaths(built).Should()
                .Equal(SkeletonFields.ConditionPaths(stated),
                    "the not-exists conditions " + of, feed.Text, name);
            SkeletonFields.ConditionEdges(built).Should()
                .Equal(SkeletonFields.ConditionEdges(stated),
                    "the edges of the not-exists conditions " + of, feed.Text, name);
            SkeletonFields.Outputs(built).Should()
                .Equal(SkeletonFields.Outputs(stated), "the outputs " + of, feed.Text, name);
        }
    }

    /// <summary>
    /// Which vectors state no feed at all. The skeleton check above reads a vector's feeds, so
    /// a vector with none passes it by reading nothing; naming them here is what keeps that
    /// from spreading unnoticed. jinaga.js drops a feed that walks no successor edge, which is
    /// every feed of these two: one projects a given, and the other walks only predecessors.
    /// </summary>
    [Fact]
    public void OnlyTheVectorsThatWalkNoSuccessorStateNoFeed()
    {
        var empty = Vectors
            .Select(row => (string)row[0])
            .Where(name => ReferenceVector.Load("feeds", name).Feeds.IsEmpty)
            .ToImmutableSortedSet();

        empty.Should().Equal("identity", "predecessor-walk-with-existential-condition");
    }

    /// <summary>
    /// The one shape whose skeleton this port would index differently from jinaga.js, and
    /// which no pinned vector has.
    ///
    /// <para>
    /// jinaga.js walks a match's conditions in the single order they are written in.
    /// <c>Match</c> holds the path conditions and the existential conditions apart, so
    /// <see cref="Skeleton.Of"/> walks all of the first and then all of the second. The two
    /// walks agree on every specification that writes its path conditions first, and only on
    /// those. <c>Skeleton</c> says as much, and this is what makes it true of the pin rather
    /// than only asserted: a re-pin that brings in an interleaved match fails here, instead
    /// of being silently indexed by an order jinaga.js did not use.
    /// </para>
    ///
    /// <para>
    /// Read off the vector JSON and not off a loaded <c>Specification</c>, because the order
    /// is exactly what loading discards: <see cref="ConformanceVector.ToSpecification"/>
    /// partitions each match's conditions into the two lists <c>Match</c> holds, and nothing
    /// in the result records which came first.
    /// </para>
    ///
    /// <para>
    /// Every match a vector states is reached: those of the vector's own specification and of
    /// each of its feeds, those of a specification nested inside an existential condition,
    /// and those of a child specification nested in a projection, which
    /// <c>ConformanceVector.ToProjection</c> loads as a <c>CollectionProjection</c> and whose
    /// matches go on from its parent's. The pin has 18 of those, so stopping at the
    /// conditions would leave the claim this test makes partly unchecked.
    /// </para>
    /// </summary>
    [Fact]
    public void NoReferenceVectorWritesAnExistentialConditionBeforeAPathCondition()
    {
        var interleaved = Vectors
            .Select(row => (string)row[0])
            .SelectMany(name =>
            {
                var json = ReferenceVector.Load("feeds", name).Json;
                return Interleaved(json.GetProperty("specification"), name)
                    .Concat(json.GetProperty("feeds").EnumerateArray().SelectMany(feed =>
                        Interleaved(
                            feed.GetProperty("specification"),
                            $"{name}, feed {feed.GetProperty("text").GetString()}")));
            })
            .ToImmutableList();

        interleaved.Should().BeEmpty();
    }

    /// <summary>
    /// Every match of a specification that states a path condition after a condition of any
    /// other kind, descending through both places a specification can nest: inside an
    /// existential condition, and inside a projection as a child specification.
    /// </summary>
    private static IEnumerable<string> Interleaved(JsonElement specification, string where) =>
        InterleavedInMatches(specification.GetProperty("matches"), where)
            .Concat(InterleavedInProjection(specification.GetProperty("projection"), where));

    private static IEnumerable<string> InterleavedInMatches(JsonElement matches, string where)
    {
        foreach (var match in matches.EnumerateArray())
        {
            var unknown = match.GetProperty("unknown").GetProperty("name").GetString();
            var existentialSeen = false;
            foreach (var condition in match.GetProperty("conditions").EnumerateArray())
            {
                var kind = condition.GetProperty("type").GetString();
                if (kind == "path")
                {
                    if (existentialSeen)
                    {
                        yield return
                            $"{where}: the match on {unknown} states a path condition after " +
                            "a condition of another kind";
                    }
                }
                else
                {
                    existentialSeen = true;
                    foreach (var nested in
                        InterleavedInMatches(condition.GetProperty("matches"), where))
                    {
                        yield return nested;
                    }
                }
            }
        }
    }

    /// <summary>
    /// A projection carries matches only where a child specification is nested in it. The
    /// kinds are the ones <c>ConformanceVector.ToProjection</c> reads, and an unrecognized
    /// one is reported rather than passed over, so a re-pin that adds a projection kind
    /// holding matches cannot slip past this walk.
    /// </summary>
    private static IEnumerable<string> InterleavedInProjection(JsonElement projection, string where)
    {
        switch (projection.GetProperty("type").GetString())
        {
            case "specification":
                return Interleaved(projection, $"{where}, child specification " +
                    $"{projection.GetProperty("name").GetString()}");
            case "composite":
                return projection.GetProperty("components").EnumerateArray()
                    .SelectMany(component => InterleavedInProjection(component, where));
            case "fact":
            case "field":
                return ImmutableList<string>.Empty;
            case var other:
                return ImmutableList.Create(
                    $"{where}: the projection kind '{other}' is one this check does not read, " +
                    "so it cannot say whether matches inside it are ordered");
        }
    }

    /// <summary>
    /// This library's feeds against jinaga.js's, for every reference vector. A vector either
    /// agrees feed for feed, compared as <c>ToDescriptiveString</c> prints them, or it is one
    /// of the known differences below.
    ///
    /// <para>
    /// The list is exact in both directions: a vector in it that now agrees fails just as a
    /// vector that newly disagrees does. A difference that gets resolved on either side
    /// therefore reaches this test at the next re-pin, rather than sitting in the list as a
    /// stale claim.
    /// </para>
    /// </summary>
    [Fact]
    public void TheFeedsEitherAgreeWithJinagaJsOrAreAKnownDifference()
    {
        var outcomes = Vectors
            .Select(row => (string)row[0])
            .Select(name => (Name: name, Outcome: Compare(name)))
            .Where(pair => pair.Outcome != Agrees)
            .ToImmutableSortedDictionary(pair => pair.Name, pair => pair.Outcome);

        outcomes.Should().Equal(KnownDifferences);
    }

    private const string Agrees = "agrees";

    /// <summary>
    /// jinaga-spec's <c>buildFeeds</c> refuses this specification, so there are no feeds to
    /// compare. jinaga.js builds some, by a rule the spec does not prove.
    /// </summary>
    private const string Refused = "refused";

    /// <summary>Both build feeds, and they are not the same feeds.</summary>
    private const string Differs = "differs";

    private static string Compare(string name)
    {
        var vector = ReferenceVector.Load("feeds", name);
        ImmutableList<Specification> feeds;
        try
        {
            feeds = FeedBuilder.BuildFeeds(vector.Specification);
        }
        catch (SpecificationException)
        {
            return Refused;
        }
        return feeds.Select(feed => feed.ToDescriptiveString())
            .SequenceEqual(vector.Feeds.Select(feed => feed.Text))
            ? Agrees
            : Differs;
    }

    /// <summary>
    /// The reference vectors whose feeds this library does not reproduce, each because
    /// jinaga-spec's <c>buildFeeds</c> and jinaga.js's disagree rather than because this port
    /// is wrong. jinaga/jinaga.js#463 tracks both groups, the <c>differs</c> and the
    /// <c>refused</c>, and <c>Conformance/README.md</c> says what each group is.
    /// </summary>
    private static readonly ImmutableSortedDictionary<string, string> KnownDifferences =
        ImmutableSortedDictionary<string, string>.Empty
            .Add("access-paths-to-configure", Differs)
            .Add("access-paths-with-configuration", Differs)
            .Add("cancelled-after-positive", Differs)
            .Add("cancelled-by-second", Differs)
            .Add("cancelled-twice", Differs)
            .Add("cancelled-with-given", Differs)
            .Add("delete-and-restore-with-projection", Differs)
            .Add("delete-and-restore-with-two-projection-components", Differs)
            .Add("events-without-unrevoked-approved-delete", Differs)
            .Add("flagged-delete", Differs)
            .Add("given-consumed-in-restore-branch", Differs)
            .Add("minimal-via-predecessor-not-exists", Differs)
            .Add("minimal-via-successors", Differs)
            .Add("mixed-positive-and-negative-existentials", Differs)
            .Add("needs-mirror", Differs)
            .Add("needs-mirror-swapped", Differs)
            .Add("production-via-compact-predecessor-not-exists", Differs)
            .Add("production-via-predecessor-not-exists", Differs)
            .Add("production-via-successors", Differs)
            .Add("random-two", Differs)
            // jinaga.js renames a repeated label and goes on; this library refuses the
            // specification, and the vector whose labels are already respelled is a feed
            // difference like the rest.
            .Add("repeated-label-with-next", Refused)
            .Add("repeated-label-with-next-respelled", Differs)
            .Add("repeated-label-without-next", Refused)
            .Add("repeated-label-without-next-respelled", Differs)
            .Add("served-and-unfrozen", Differs)
            .Add("served-and-unfrozen-swapped", Differs)
            .Add("served-and-unfrozen-with-regions", Differs)
            .Add("sibling-negative-after-positive", Differs)
            .Add("three-level-restore-with-projection", Differs);
}
