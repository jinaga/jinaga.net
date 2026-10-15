using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Jinaga.Cryptography;
using Jinaga.DefaultImplementations;
using Jinaga.Facts;
using Jinaga.Http;
using Jinaga.Serialization;
using Jinaga.Services;
using Jinaga.Store.SQLite.Test.Fakes;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test;

public class DateLearnedTest
{
    private readonly ITestOutputHelper output;
    private readonly SQLiteStore store;
    private readonly JinagaClient j;

    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "DateLearnedTest.db");

    public DateLearnedTest(ITestOutputHelper output)
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        this.output = output;
        store = new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
        j = new JinagaClient(store, new LocalNetwork(), [], NullLoggerFactory.Instance, new JinagaClientOptions());
    }

    private static readonly Specification<Site, PostWithDateLearned> postWithDateLearned =
        Given<Site>.Match((site, facts) =>
            from post in facts.OfType<Post>()
            where post.site == site
            select new PostWithDateLearned
            {
                post = post,
                learned = facts.DateLearned(post)
            }
        );

    [Fact]
    public async Task DateLearned_IsTheInstantOfTheFirstSave()
    {
        var site = await j.Fact(new Site("contoso.com"));

        var before = DateTime.UtcNow;
        await j.Fact(new Post(site, "2026-10-13"));
        var after = DateTime.UtcNow;

        var learned = await WhenQueryDateLearned(site);

        learned.Kind.Should().Be(DateTimeKind.Utc);
        // The date_learned column defaults to CURRENT_TIMESTAMP, which SQLite writes to the
        // second, so the save instant is truncated rather than rounded.
        learned.Should().BeOnOrAfter(TruncateToSecond(before))
            .And.BeOnOrBefore(TruncateToSecond(after));

        await j.Unload();
    }

    [Fact]
    public async Task DateLearned_DoesNotChangeWhenTheFactIsSavedAgain()
    {
        var site = await j.Fact(new Site("contoso.com"));
        var post = new Post(site, "2026-10-13");
        await j.Fact(post);

        var learned = await WhenQueryDateLearned(site);

        // date_learned has one-second resolution, so a re-save within the same second could
        // re-stamp it with the same value and go unnoticed. Wait until the clock has moved
        // past that second, so that any re-stamp shows up as a different date.
        await WhenTheClockPasses(learned);

        await j.Fact(new Post(site, "2026-10-13"));
        var afterPlainResave = await WhenQueryDateLearned(site);
        afterPlainResave.Should().Be(learned);

        // The same fact again, this time carrying a signature it did not have before.
        await ((IStore)store).Save(GivenSignedGraph(post), false, CancellationToken.None);
        var afterSignedResave = await WhenQueryDateLearned(site);
        afterSignedResave.Should().Be(learned);

        await j.Unload();
    }

    [Fact]
    public async Task DateLearned_OfAFactFromTheNetworkIsWhenThisReplicaSavedIt()
    {
        var site = new Site("contoso.com");
        var network = new FakeNetwork(output);
        network.AddFeed("posts", new object[]
        {
            new Post(site, "2026-10-13")
        });
        var networkClient = new JinagaClient(store, network, [], NullLoggerFactory.Instance, new JinagaClientOptions());

        // The fact already existed upstream before this window opened, so a date inside it
        // can only have been recorded by the local store as the fetch saved the fact.
        var before = DateTime.UtcNow;
        var results = await networkClient.Query(postWithDateLearned, site);
        var after = DateTime.UtcNow;

        var learned = results.Should().ContainSingle().Subject.learned;
        learned.Kind.Should().Be(DateTimeKind.Utc);
        learned.Should().BeOnOrAfter(TruncateToSecond(before))
            .And.BeOnOrBefore(TruncateToSecond(after));

        await networkClient.Unload();
    }

    [Fact]
    public async Task DateLearned_IsAbsentFromTheSerializedGraph()
    {
        var site = await j.Fact(new Site("contoso.com"));
        var post = new Post(site, "2026-10-13");
        await j.Fact(post);

        var learned = await WhenQueryDateLearned(site);

        // Load the same facts back out of the store and serialize them the way the client
        // sends them upstream.
        var references = ImmutableList.Create(new FactReference("Blog.Post", j.Hash(post)));
        var loadedGraph = await ((IStore)store).Load(references, CancellationToken.None);
        var serializedFromStore = WhenSerializeGraph(loadedGraph);

        // A graph assembled without ever visiting a store cannot carry a date learned, so
        // anything the store added would show up as a difference.
        var serializedFromCollector = WhenSerializeGraph(GivenCollectedGraph(post));

        serializedFromStore.Should().Be(serializedFromCollector);
        serializedFromStore.Should().NotContain(learned.ToString("yyyy-MM-dd HH:mm:ss"));
        serializedFromStore.Should().NotContain(learned.ToString("o"));

        await j.Unload();
    }

    [Fact]
    public void DateLearned_CannotBeUsedInAWhereClause()
    {
        var cutoff = DateTime.UtcNow;

        Action declaring = () => Given<Site>.Match((site, facts) =>
            from post in facts.OfType<Post>()
            where post.site == site
            where facts.DateLearned(post) == cutoff
            select post
        );

        declaring.Should().Throw<SpecificationException>();
    }

    [Fact]
    public void DateLearned_CannotBeUsedInAnExistentialCondition()
    {
        var cutoff = DateTime.UtcNow;

        Action declaring = () => Given<Site>.Match((site, facts) =>
            from post in facts.OfType<Post>()
            where post.site == site
            where facts.Any<Title>(title => facts.DateLearned(title) == cutoff)
            select post
        );

        declaring.Should().Throw<SpecificationException>();
    }

    [Fact]
    public void DateLearned_DistinguishesTwoSpecifications()
    {
        postWithDateLearned.ToDescriptiveString()
            .Should().NotBe(postWithoutDateLearned.ToDescriptiveString());
    }

    [Fact]
    public void DateLearned_DoesNotReachTheFeedsRequest()
    {
        var givenTuple = FactReferenceTuple.Empty.Add(
            "site",
            new FactReference("Blog.Site", "--- SITE HASH ---"));

        HttpNetwork.CreateFeedsRequest(givenTuple, postWithDateLearned)
            .Should().Be(HttpNetwork.CreateFeedsRequest(givenTuple, postWithoutDateLearned));
    }

    private static readonly Specification<Site, PostWithoutDateLearned> postWithoutDateLearned =
        Given<Site>.Match((site, facts) =>
            from post in facts.OfType<Post>()
            where post.site == site
            select new PostWithoutDateLearned
            {
                post = post
            }
        );

    private async Task<DateTime> WhenQueryDateLearned(Site site)
    {
        var results = await j.Query(postWithDateLearned, site);
        return results.Should().ContainSingle().Subject.learned;
    }

    private static async Task WhenTheClockPasses(DateTime instant)
    {
        while (TruncateToSecond(DateTime.UtcNow) <= TruncateToSecond(instant))
        {
            await Task.Delay(50);
        }
    }

    private static DateTime TruncateToSecond(DateTime value) =>
        new DateTime(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);

    private static FactGraph GivenCollectedGraph(object fact)
    {
        var collector = new Collector(SerializerCache.Empty, new ConditionalWeakTable<object, FactGraph>());
        collector.Serialize(fact);
        return collector.Graph;
    }

    /// <summary>
    /// The same facts, with the topmost one signed by a key this client has never seen.
    /// </summary>
    private static FactGraph GivenSignedGraph(object fact)
    {
        var graph = GivenCollectedGraph(fact);
        var keyPair = KeyPair.Generate();
        var last = graph.Last;
        var signedGraph = FactGraph.Empty;
        foreach (var reference in graph.FactReferences)
        {
            var member = graph.GetFact(reference);
            var signatures = reference.Equals(last)
                ? ImmutableList.Create(keyPair.SignFact(member))
                : ImmutableList<FactSignature>.Empty;
            signedGraph = signedGraph.Add(new FactEnvelope(member, signatures));
        }
        return signedGraph;
    }

    private static string WhenSerializeGraph(FactGraph graph)
    {
        using var stream = new MemoryStream();
        using (var serializer = new GraphSerializer(stream))
        {
            serializer.Serialize(graph);
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private class PostWithDateLearned
    {
        public Post post { get; set; } = null!;
        public DateTime learned { get; set; }
    }

    private class PostWithoutDateLearned
    {
        public Post post { get; set; } = null!;
    }
}
