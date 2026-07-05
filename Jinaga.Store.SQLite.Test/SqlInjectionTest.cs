using Jinaga.DefaultImplementations;
using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Services;
using Jinaga.Storage;
using Jinaga.Store.SQLite.Test.Model.SqlInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;

namespace Jinaga.Store.SQLite.Test;

/// <summary>
/// Regression tests for https://github.com/jinaga/jinaga.net/issues/185.
///
/// Fact type names (from [FactType(...)] attributes) and other identifiers such as
/// feed hashes are arbitrary strings that may originate from the network (facts
/// loaded from a replicator). Several SQLiteStore code paths used to build SQL by
/// string interpolation/concatenation instead of parameter binding, which broke
/// (or could be exploited) when those strings contained a single quote or SQL
/// comment syntax such as "--".
/// </summary>
public class SqlInjectionTest
{
    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "SqlInjectionTest.db");

    private static SQLiteStore GivenSQLiteStore()
    {
        if (File.Exists(SQLitePath))
        {
            File.Delete(SQLitePath);
        }
        return new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
    }

    [Fact]
    public async Task SaveAndLoadFact_WithQuoteAndCommentInFactType_RoundTrips()
    {
        // Both the fact type and its predecessor's fact type contain a single quote
        // and a SQL line-comment marker. This exercises getFactId, which is invoked
        // for every predecessor on every save.
        IStore store = GivenSQLiteStore();
        var options = new JinagaClientOptions();
        var jinagaClient = new JinagaClient(store, new Jinaga.DefaultImplementations.LocalNetwork(), [], NullLoggerFactory.Instance, options);

        var order = await jinagaClient.Fact(new MaliciousOrder("order-1"));
        var item = await jinagaClient.Fact(new MaliciousOrderItem(order, "sku-1"));

        var specification = Given<MaliciousOrder>.Match((o, facts) =>
            from i in facts.OfType<MaliciousOrderItem>()
            where i.order == o
            select i.sku
        );

        var skus = await jinagaClient.Query(specification, order);

        skus.Should().ContainSingle().Which.Should().Be("sku-1");
    }

    [Fact]
    public async Task ListKnown_WithQuoteAndCommentInFactType_DoesNotBreakQuery()
    {
        IStore store = GivenSQLiteStore();
        var options = new JinagaClientOptions();
        var jinagaClient = new JinagaClient(store, new LocalNetwork(), [], NullLoggerFactory.Instance, options);

        var order = await jinagaClient.Fact(new MaliciousOrder("order-2"));
        var orderReference = ReferenceOfFact(order);

        var unknownReference = new FactReference(orderReference.Type, "definitely-unknown-hash");

        // Directly exercise IStore.ListKnown with a type name containing a quote and comment.
        var references = ImmutableList<FactReference>.Empty
            .Add(orderReference)
            .Add(unknownReference);

        var known = await store.ListKnown(references);

        known.Should().Contain(r => r.Hash == orderReference.Hash && r.Type == orderReference.Type);
        known.Should().NotContain(r => r.Hash == unknownReference.Hash);
    }

    private static FactReference ReferenceOfFact(object fact)
    {
        var store = new MemoryStore();
        var loggerFactory = NullLoggerFactory.Instance;
        var networkManager = new NetworkManager(new LocalNetwork(), store, loggerFactory, (FactGraph g, ImmutableList<Fact> l, CancellationToken c) => Task.CompletedTask);
        var factManager = new FactManager(store, networkManager, [], loggerFactory, 0);
        var graph = factManager.Serialize(fact);
        var lastRef = graph.Last;
        return lastRef;
    }

    [Fact]
    public async Task SaveAndLoadBookmark_WithQuoteAndCommentInFeed_RoundTrips()
    {
        IStore store = GivenSQLiteStore();

        string feed = "feed'with--quote";
        await store.SaveBookmark(feed, "bookmark-1");
        var bookmark = await store.LoadBookmark(feed);

        bookmark.Should().Be("bookmark-1");

        // A different feed hash sharing the same prefix should not be confused with the first.
        var unknownBookmark = await store.LoadBookmark("feed'with--quote-other");
        unknownBookmark.Should().Be("");
    }
}
