using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
/// (<see cref="FeedBuilder"/>, and the decision recorded on jinaga/jinaga.net#201). So
/// <see cref="TheFeedsEitherAgreeWithJinagaJsOrAreAKnownDifference"/> states which vectors
/// agree and which do not, and <c>Conformance/README.md</c> carries the issue filed for each
/// difference. Neither side is patched here to make them agree.
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
    /// </summary>
    [Theory]
    [MemberData(nameof(Vectors))]
    public void ThePortBuildsTheSkeletonOfEveryReferenceFeed(string name)
    {
        var vector = ReferenceVector.Load("feeds", name);

        foreach (var feed in vector.Feeds)
        {
            Skeleton.Of(feed.Specification).Should().Be(
                feed.Skeleton,
                "the skeleton of the feed {0} of vector '{1}'", feed.Text, name);
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
    /// is wrong. <c>Conformance/README.md</c> names the issue filed for each group.
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
