using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Storage;

namespace Jinaga.Test.Conformance;

/// <summary>
/// The feed vectors of jinaga-spec: the feeds its proven <c>buildFeeds</c> makes of a
/// specification, and what they deliver on a graph a replicator holds.
///
/// These vectors gate the port two ways. They state the feeds themselves, which
/// <see cref="FeedBuilder"/> must build in order, and they state what the feeds come to on a
/// graph, independently of what builds them: the second check reads every feed through
/// <see cref="MemoryStore"/> and compares the facts its tuples bind against the vector, so a
/// port that agrees with the expected text and delivers the wrong facts still fails.
///
/// The pinned vectors are the only statement in this repository of what the feeds are. No test
/// here writes an expected feed by hand.
/// </summary>
public class FeedVectorTest
{
    public static IEnumerable<object[]> Vectors => ConformanceVector.Names("feeds");

    [Theory]
    [MemberData(nameof(Vectors))]
    public void ThePortBuildsTheFeeds(string name)
    {
        var vector = ConformanceVector.Load("feeds", name);

        var feeds = FeedBuilder.BuildFeeds(vector.Specification);

        feeds.Select(feed => feed.ToDescriptiveString()).Should().Equal(ExpectedTexts(vector));
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public async Task TheFeedsDeliverTheExpectedFacts(string name)
    {
        var vector = ConformanceVector.Load("feeds", name);

        var delivered = await Deliver(vector, FeedBuilder.BuildFeeds(vector.Specification));

        delivered.Should().Equal(Delivered(vector));
    }

    [Fact]
    public void BuildsNoFeedForASpecificationThatWalksOnlyPredecessors()
    {
        var vector = ConformanceVector.Load("feeds", "predecessors-only");

        FeedBuilder.BuildFeeds(vector.Specification).Should().BeEmpty();
    }

    /// <summary>The text of each expected feed, in order.</summary>
    private static ImmutableList<string> ExpectedTexts(ConformanceVector vector) =>
        vector.Expected.GetProperty("feeds").EnumerateArray()
            .Select(feed => feed.GetProperty("text").GetString()!)
            .ToImmutableList();

    /// <summary>
    /// Every fact a tuple of a feed binds to one of the feed's unknowns, read from the
    /// vector's givens on its graph. A fact is delivered once however many feeds and tuples
    /// name it, so this is the set the vector states, in the order the vector writes it.
    /// </summary>
    private static async Task<ImmutableList<int>> Deliver(
        ConformanceVector vector, ImmutableList<Specification> feeds)
    {
        var (store, references) = ConformanceVector.PlaceGraph(vector.Graph);
        var ids = references.ToImmutableDictionary(pair => pair.Value.Hash, pair => pair.Key);
        var givens = vector.Givens;
        var delivered = ImmutableSortedSet<int>.Empty;
        foreach (var feed in feeds)
        {
            var start = feed.Givens.Aggregate(
                FactReferenceTuple.Empty,
                (tuple, given) => tuple.Add(given.Label.Name, references[givens[given.Label.Name]]));
            var products = await store.Read(start, feed, CancellationToken.None);
            delivered = delivered.Union(products.SelectMany(product => feed.Matches
                .Select(match => ids[product.GetFactReference(match.Unknown.Name).Hash])));
        }
        return delivered.ToImmutableList();
    }

    private static ImmutableList<int> Delivered(ConformanceVector vector) =>
        vector.Expected.GetProperty("delivered").EnumerateArray()
            .Select(id => id.GetInt32())
            .ToImmutableList();
}
