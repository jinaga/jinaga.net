#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Jinaga.Cryptography;
using Jinaga.DefaultImplementations;
using Jinaga.Facts;
using Jinaga.Http;
using Jinaga.Products;
using Jinaga.Repository;
using Jinaga.Serialization;
using Jinaga.Services;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Jinaga.Test.Specifications;

// Jinaga.Test declares its own Site, and a member of an enclosing namespace wins over a
// using directive, so name these deliberately.
using Comment = Jinaga.Test.Model.Comment;
using Content = Jinaga.Test.Model.Content;
using Publish = Jinaga.Test.Model.Publish;
using Site = Jinaga.Test.Model.Site;

public class DateLearnedTest
{
    private readonly ITestOutputHelper output;

    public DateLearnedTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    private static readonly Specification<Site, ContentWithDateLearned> contentWithDateLearned =
        Given<Site>.Match((site, facts) =>
            from content in facts.OfType<Content>()
            where content.site == site
            select new ContentWithDateLearned
            {
                content = content,
                learned = facts.DateLearned(content)
            }
        );

    [Fact]
    public async Task DateLearned_IsTheInstantOfTheFirstSave()
    {
        var store = new MemoryStore();
        var j = GivenJinagaClient(store);
        var site = await GivenSite(j);

        var before = DateTime.UtcNow;
        await j.Fact(new Content(site, "/first"));
        var after = DateTime.UtcNow;

        var learned = await WhenQueryDateLearned(j, site);

        learned.Kind.Should().Be(DateTimeKind.Utc);
        learned.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task DateLearned_DoesNotChangeWhenTheFactIsSavedAgain()
    {
        var store = new MemoryStore();
        var j = GivenJinagaClient(store);
        var site = await GivenSite(j);
        var content = new Content(site, "/first");
        await j.Fact(content);

        var learned = await WhenQueryDateLearned(j, site);

        await j.Fact(new Content(site, "/first"));
        var afterPlainResave = await WhenQueryDateLearned(j, site);
        afterPlainResave.Should().Be(learned);

        // The same fact again, this time carrying a signature it did not have before.
        // Signatures are merged onto a fact that is already here, so it is not new.
        await store.Save(GivenSignedGraph(content), false, CancellationToken.None);
        var afterSignedResave = await WhenQueryDateLearned(j, site);
        afterSignedResave.Should().Be(learned);
    }

    [Fact]
    public async Task DateLearned_OfAFactFromTheNetworkIsWhenThisReplicaSavedIt()
    {
        var creator = new User("--- OTHER REPLICA ---");
        var site = new Site(creator, "contoso");
        var network = new FakeNetwork(output);
        network.AddFeed("content", new object[]
        {
            new Content(site, "/from-the-network")
        });
        var j = GivenJinagaClient(new MemoryStore(), network);

        // The fact already existed upstream before this window opened, so a date inside it
        // can only have been recorded by the local store as the fetch saved the fact.
        var before = DateTime.UtcNow;
        var results = await j.Query(contentWithDateLearned, site);
        var after = DateTime.UtcNow;

        var learned = results.Should().ContainSingle().Subject.learned;
        learned.Kind.Should().Be(DateTimeKind.Utc);
        learned.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task DateLearned_IsAbsentFromTheSerializedGraph()
    {
        var store = new MemoryStore();
        var j = GivenJinagaClient(store);
        var site = await GivenSite(j);
        var content = new Content(site, "/first");
        await j.Fact(content);

        var learned = await WhenQueryDateLearned(j, site);

        // Load the same facts back out of the store and serialize them the way the client
        // sends them upstream.
        var references = ImmutableList.Create(new FactReference("Blog.Content", j.Hash(content)));
        var loadedGraph = await ((Jinaga.Services.IStore)store).Load(references, CancellationToken.None);
        var serializedFromStore = WhenSerializeGraph(loadedGraph);

        // A graph assembled without ever visiting a store cannot carry a date learned, so
        // anything the store added would show up as a difference.
        var serializedFromCollector = WhenSerializeGraph(GivenCollectedGraph(content));

        serializedFromStore.Should().Be(serializedFromCollector);
        serializedFromStore.Should().NotContain(learned.ToString("yyyy-MM-dd"));
        serializedFromStore.Should().NotContain(learned.ToString("o"));
    }

    [Fact]
    public async Task DateLearned_IsProjectedInsideACollection()
    {
        var store = new MemoryStore();
        var j = GivenJinagaClient(store);
        var creator = await j.Fact(new User("--- PUBLIC KEY ---"));
        var site = await j.Fact(new Site(creator, "contoso"));
        var content = await j.Fact(new Content(site, "/first"));

        var before = DateTime.UtcNow;
        await j.Fact(new Comment(content, Guid.NewGuid(), creator));
        var after = DateTime.UtcNow;

        var results = await j.Query(Given<Site>.Match((site, facts) =>
            from content in facts.OfType<Content>()
            where content.site == site
            select new
            {
                content,
                comments = facts.Observable(content, commentsOfContent)
            }
        ), site);

        var learned = results.Should().ContainSingle().Subject
            .comments.Should().ContainSingle().Subject.learned;
        learned.Kind.Should().Be(DateTimeKind.Utc);
        learned.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    private static readonly Specification<Content, CommentWithDateLearned> commentsOfContent =
        Given<Content>.Match((content, facts) =>
            from comment in facts.OfType<Comment>()
            where comment.content == content
            select new CommentWithDateLearned
            {
                comment = comment,
                learned = facts.DateLearned(comment)
            }
        );

    [Fact]
    public void DateLearned_CannotBeUsedInAWhereClause()
    {
        var cutoff = DateTime.UtcNow;

        Action declaring = () => Given<Site>.Match((site, facts) =>
            from content in facts.OfType<Content>()
            where content.site == site
            where facts.DateLearned(content) == cutoff
            select content
        );

        declaring.Should().Throw<SpecificationException>()
            .WithMessage("*cannot affect matching*");
    }

    [Fact]
    public void DateLearned_CannotBeComparedInAWhereClause()
    {
        var cutoff = DateTime.UtcNow;

        Action declaring = () => Given<Site>.Match((site, facts) =>
            from content in facts.OfType<Content>()
            where content.site == site
            where facts.DateLearned(content) > cutoff
            select content
        );

        declaring.Should().Throw<SpecificationException>()
            .WithMessage("*cannot affect matching*");
    }

    [Fact]
    public void DateLearned_CannotBeUsedInAnExistentialCondition()
    {
        var cutoff = DateTime.UtcNow;

        Action declaring = () => Given<Site>.Match((site, facts) =>
            from content in facts.OfType<Content>()
            where content.site == site
            where facts.Any<Publish>(publish => facts.DateLearned(publish) == cutoff)
            select content
        );

        declaring.Should().Throw<SpecificationException>()
            .WithMessage("*cannot affect matching*");
    }

    [Fact]
    public async Task DateLearned_IsProjectedForAFactSavedWhileWatching()
    {
        var store = new MemoryStore();
        var j = GivenJinagaClient(store);
        var site = await GivenSite(j);
        var observed = new List<ContentWithDateLearned>();
        var observer = j.Watch(contentWithDateLearned, site,
            projection => observed.Add(projection));
        await observer.Loaded;

        var before = DateTime.UtcNow;
        await j.Fact(new Content(site, "/first"));
        var after = DateTime.UtcNow;
        observer.Stop();

        var watched = observed.Should().ContainSingle().Subject;
        watched.learned.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        watched.learned.Should().Be(await WhenQueryDateLearned(j, site));
    }

    [Fact]
    public async Task DateLearned_FromAStoreThatReportsNoDates_FailsOnlyTheSpecificationThatProjectsIt()
    {
        var j = GivenJinagaClient(new StoreWithoutDatesLearned(new MemoryStore()));
        var site = await GivenSite(j);
        await j.Fact(new Content(site, "/first"));

        var withoutDates = await j.Query(contentWithoutDateLearned, site);
        withoutDates.Should().ContainSingle();

        Func<Task> queryingDates = () => j.Query(contentWithDateLearned, site);
        await queryingDates.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*did not report*");
    }

    [Fact]
    public void DateLearned_DistinguishesTwoSpecifications()
    {
        contentWithDateLearned.ToDescriptiveString()
            .Should().NotBe(contentWithoutDateLearned.ToDescriptiveString());
    }

    [Fact]
    public void DateLearned_DoesNotReachTheFeedsRequest()
    {
        var givenTuple = FactReferenceTuple.Empty.Add(
            "site",
            new FactReference("Blog.Site", "--- SITE HASH ---"));

        HttpNetwork.CreateFeedsRequest(givenTuple, contentWithDateLearned)
            .Should().Be(HttpNetwork.CreateFeedsRequest(givenTuple, contentWithoutDateLearned));
    }

    private static readonly Specification<Site, ContentWithoutDateLearned> contentWithoutDateLearned =
        Given<Site>.Match((site, facts) =>
            from content in facts.OfType<Content>()
            where content.site == site
            select new ContentWithoutDateLearned
            {
                content = content
            }
        );

    private static JinagaClient GivenJinagaClient(IStore store, Jinaga.Services.INetwork? network = null)
    {
        return new JinagaClient(
            store,
            network ?? new LocalNetwork(),
            ImmutableList<Jinaga.Projections.Specification>.Empty,
            NullLoggerFactory.Instance,
            new JinagaClientOptions());
    }

    private static async Task<Site> GivenSite(JinagaClient j)
    {
        var creator = await j.Fact(new User("--- PUBLIC KEY ---"));
        return await j.Fact(new Site(creator, "contoso"));
    }

    private static async Task<DateTime> WhenQueryDateLearned(JinagaClient j, Site site)
    {
        var results = await j.Query(contentWithDateLearned, site);
        return results.Should().ContainSingle().Subject.learned;
    }

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

    private class ContentWithDateLearned
    {
        public Content content { get; set; } = null!;
        public DateTime learned { get; set; }
    }

    private class ContentWithoutDateLearned
    {
        public Content content { get; set; } = null!;
    }

    private class CommentWithDateLearned
    {
        public Comment comment { get; set; } = null!;
        public DateTime learned { get; set; }
    }

    /// <summary>
    /// A store written before <see cref="IStore.LoadDatesLearned"/> existed: it forwards every
    /// member it knows about and leaves that one to the interface's default.
    /// </summary>
    private class StoreWithoutDatesLearned : IStore
    {
        private readonly IStore inner;

        public StoreWithoutDatesLearned(IStore inner)
        {
            this.inner = inner;
        }

        public bool IsPersistent => inner.IsPersistent;

        public Task<ImmutableList<Fact>> Save(FactGraph graph, bool queue, CancellationToken cancellationToken) =>
            inner.Save(graph, queue, cancellationToken);

        public Task<ImmutableList<Product>> Read(FactReferenceTuple givenTuple, Jinaga.Projections.Specification specification, CancellationToken cancellationToken) =>
            inner.Read(givenTuple, specification, cancellationToken);

        public Task<FactGraph> Load(ImmutableList<FactReference> references, CancellationToken cancellationToken) =>
            inner.Load(references, cancellationToken);

        public Task<string> LoadBookmark(string feed) => inner.LoadBookmark(feed);

        public Task<ImmutableList<FactReference>> ListKnown(ImmutableList<FactReference> factReferences) =>
            inner.ListKnown(factReferences);

        public Task SaveBookmark(string feed, string bookmark) => inner.SaveBookmark(feed, bookmark);

        public Task<DateTime?> GetMruDate(string specificationHash) => inner.GetMruDate(specificationHash);

        public Task SetMruDate(string specificationHash, DateTime mruDate) => inner.SetMruDate(specificationHash, mruDate);

        public Task<QueuedFacts> GetQueue() => inner.GetQueue();

        public Task SetQueueBookmark(string bookmark) => inner.SetQueueBookmark(bookmark);

        public Task<IEnumerable<Fact>> GetAllFacts() => inner.GetAllFacts();

        public Task Purge(ImmutableList<Jinaga.Projections.Specification> purgeConditions) => inner.Purge(purgeConditions);

        public Task PurgeDescendants(FactReference purgeRoot, ImmutableList<FactReference> triggers) =>
            inner.PurgeDescendants(purgeRoot, triggers);
    }
}
