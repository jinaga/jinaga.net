using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Jinaga.Test.Managers;

/// <summary>
/// A store compiled against Jinaga 1.1.54 or earlier, such as the published
/// Jinaga.Store.SQLite 1.1.19, builds its queue with QueuedFacts(FactGraph, string)
/// and implements neither LoadSavers nor AcknowledgeQueue. NuGet lets it run
/// against this version, so the queue it builds must still drain.
/// </summary>
public class LegacyQueueStoreTest
{
    private const int DelayLongerThanAnyTest = 60000;

    /// <summary>
    /// Behaves as a store built against the old API: one graph and one
    /// bookmark, and the interface's default LoadSavers and AcknowledgeQueue.
    /// </summary>
    private class LegacyQueueStore : MemoryStore, IStore
    {
        public List<string> Bookmarks { get; } = new List<string>();

        public new bool IsPersistent => true;

        async Task<QueuedFacts> IStore.GetQueue()
        {
            var queue = await GetQueue();
            var nextBookmark = queue.Facts.IsEmpty ? "0" : queue.Facts[queue.Facts.Count - 1].Bookmark;
#pragma warning disable CS0618 // The obsolete constructor is what an old store calls
            return new QueuedFacts(queue.Graph, nextBookmark);
#pragma warning restore CS0618
        }

        Task IStore.SetQueueBookmark(string bookmark)
        {
            Bookmarks.Add(bookmark);
            return SetQueueBookmark(bookmark);
        }

        Task<ImmutableDictionary<FactReference, ImmutableHashSet<string>>> IStore.LoadSavers(ImmutableList<FactReference> references, CancellationToken cancellationToken)
            => Task.FromResult(ImmutableDictionary<FactReference, ImmutableHashSet<string>>.Empty);

        // The interface's default, which an old store inherits.
        Task IStore.AcknowledgeQueue(ImmutableList<QueuedFact> facts)
            => facts.IsEmpty
                ? Task.CompletedTask
                : ((IStore)this).SetQueueBookmark(facts[facts.Count - 1].Bookmark);
    }

    private static JinagaClient CreateClient(LegacyQueueStore store, RecordingFakeNetwork network)
    {
        var options = new JinagaClientOptions
        {
            MaxBatchSize = 10,
            QueueProcessingDelay = DelayLongerThanAnyTest
        };
        return new JinagaClient(
            store,
            network,
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            options);
    }

    [Fact]
    public async Task Push_FromALegacyStore_SendsTheWholeQueueInOneRequest()
    {
        var store = new LegacyQueueStore();
        var network = new RecordingFakeNetwork();
        var j = CreateClient(store, network);

        for (int i = 0; i < 25; i++)
        {
            await j.Fact(new TestFact($"fact{i}"));
        }

        await j.Push();

        network.SavedGraphs.Select(graph => graph.FactReferences.Count)
            .Should().Equal(new[] { 25 }, "a single bookmark cannot acknowledge part of the queue");
        store.Bookmarks.Should().Equal("25");
    }

    [Fact]
    public async Task Push_FromALegacyStore_EmptiesTheQueue()
    {
        var store = new LegacyQueueStore();
        var network = new RecordingFakeNetwork();
        var j = CreateClient(store, network);

        for (int i = 0; i < 25; i++)
        {
            await j.Fact(new TestFact($"fact{i}"));
        }

        await j.Push();
        await j.Push();

        network.SavedGraphs.Should().HaveCount(1, "the first push acknowledged everything");
    }

    [Fact]
    public async Task Push_FromALegacyStore_WhenTheSendFails_KeepsTheWholeQueue()
    {
        var store = new LegacyQueueStore();
        var network = new RecordingFakeNetwork();
        network.FailSaveCall(1);
        var j = CreateClient(store, network);

        for (int i = 0; i < 25; i++)
        {
            await j.Fact(new TestFact($"fact{i}"));
        }

        Func<Task> failingPush = () => j.Push();
        await failingPush.Should().ThrowAsync<InvalidOperationException>();
        store.Bookmarks.Should().BeEmpty();

        await j.Push();

        network.SavedGraphs.Select(graph => graph.FactReferences.Count)
            .Should().Equal(25);
    }

    [Fact]
    public void NextBookmark_FromTheLegacyConstructor_IsTheBookmarkItWasGiven()
    {
#pragma warning disable CS0618
        var queue = new QueuedFacts(FactGraph.Empty, "42");
        queue.NextBookmark.Should().Be("42");
#pragma warning restore CS0618
        queue.Facts.Should().BeEmpty();
    }
}
