using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Serialization;
using Jinaga.Services;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Jinaga.Test.Managers;

/// <summary>
/// What a feed response notifies. A feed names the facts it has reached, which
/// includes facts the store already holds, and the graph that answers a load for
/// them carries their predecessors, which the response does not name. Neither of
/// those sets is the set of facts the save added, and the inverses read only the
/// facts the save added.
/// </summary>
public class SaveAndNotifyTest
{
    /// <summary>
    /// A specification that matches its own given and projects it, so a listener
    /// on it receives one row for each airline that is notified.
    /// </summary>
    private static readonly Projections.Specification AirlineItself =
        MatchingItsGiven("Skylane.Airline", typeof(Airline));

    /// <summary>
    /// The same, for an airline day.
    /// </summary>
    private static readonly Projections.Specification AirlineDayItself =
        MatchingItsGiven("Skylane.Airline.Day", typeof(AirlineDay));

    [Fact]
    public async Task FeedResponse_NamingKnownAndNewFacts_NotifiesOnlyWhatTheSaveAdded()
    {
        var network = new ScriptedStreamNetwork();
        var store = new MemoryStore();
        var factManager = CreateFactManager(store);

        // The graph that answers the load: an airline and a day the store already
        // holds, a day that is new, and that new day's airline, which the response
        // does not name.
        var general = new Airline("general");
        var knownDay = new AirlineDay(general, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        var lounge = new Airline("lounge");
        var newDay = new AirlineDay(lounge, new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc));
        var responseGraph = GraphOf(general, knownDay, lounge, newDay);
        var generalReference = ReferenceOf(general);
        var knownDayReference = ReferenceOf(knownDay);
        var loungeReference = ReferenceOf(lounge);
        var newDayReference = ReferenceOf(newDay);
        network.ResponseGraph = responseGraph;
        network.ResponseReferences = ImmutableList.Create(
            generalReference, knownDayReference, newDayReference);

        await store.Save(GraphOf(general, knownDay), false, default);

        var airlines = new DeliveredFacts();
        var days = new DeliveredFacts();
        factManager.AddSpecificationListener(AirlineItself, airlines.OnResult);
        factManager.AddSpecificationListener(AirlineDayItself, days.OnResult);

        var subscriber = GivenSubscriber(network, store,
            (graph, cancellationToken) => factManager.SaveAndNotify(graph, false, null, cancellationToken));
        try
        {
            await subscriber.Start();
        }
        finally
        {
            subscriber.Stop();
        }

        // The new airline is a predecessor the response did not name, and it was added,
        // so it is notified. The airline the store already held was named, and it was
        // not added, so it is not.
        airlines.References.Should().Equal(loungeReference);
        days.References.Should().Equal(newDayReference);
    }

    [Fact]
    public async Task FeedResponse_WhoseReferencesAreAllKnown_SavesNothingAndNotifiesNothing()
    {
        var network = new ScriptedStreamNetwork();
        var store = new MemoryStore();
        var factManager = CreateFactManager(store);

        var general = new Airline("general");
        var knownDay = new AirlineDay(general, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        network.ResponseGraph = GraphOf(general, knownDay);
        network.ResponseReferences = ImmutableList.Create(
            ReferenceOf(general), ReferenceOf(knownDay));

        await store.Save(GraphOf(general, knownDay), false, default);

        var airlines = new DeliveredFacts();
        var days = new DeliveredFacts();
        factManager.AddSpecificationListener(AirlineItself, airlines.OnResult);
        factManager.AddSpecificationListener(AirlineDayItself, days.OnResult);

        int saves = 0;
        var subscriber = GivenSubscriber(network, store, (graph, cancellationToken) =>
        {
            Interlocked.Increment(ref saves);
            return factManager.SaveAndNotify(graph, false, null, cancellationToken);
        });
        try
        {
            await subscriber.Start();
        }
        finally
        {
            subscriber.Stop();
        }

        saves.Should().Be(0);
        airlines.Calls.Should().Be(0);
        days.Calls.Should().Be(0);
        // The bookmark still advances, so the response is not delivered again.
        (await store.LoadBookmark("feed-1")).Should().Be("bookmark-1");
    }

    private static Subscriber GivenSubscriber(
        ScriptedStreamNetwork network,
        MemoryStore store,
        Func<FactGraph, CancellationToken, Task> saveAndNotify)
    {
        return new Subscriber(
            "feed-1",
            network,
            store,
            NullLogger.Instance,
            saveAndNotify,
            reconnectInitialDelay: TimeSpan.FromMilliseconds(20),
            reconnectMaxDelay: TimeSpan.FromMilliseconds(200));
    }

    private static FactManager CreateFactManager(IStore store)
    {
        var loggerFactory = NullLoggerFactory.Instance;
        var networkManager = new NetworkManager(
            new SimulatedNetwork(null),
            store,
            loggerFactory,
            (FactGraph graph, CancellationToken cancellationToken) => Task.CompletedTask,
            new JinagaClientOptions().MaxBatchSize);
        return new FactManager(store, networkManager, ImmutableList<Projections.Specification>.Empty, loggerFactory, 0);
    }

    /// <summary>
    /// A specification whose one given is matched by its one unknown, so reading it
    /// with a fact as the given returns that fact. A listener on it receives one row
    /// for each fact of that type that is notified.
    /// </summary>
    private static Projections.Specification MatchingItsGiven(string type, Type projected)
    {
        return new Projections.Specification(
            ImmutableList.Create(
                new SpecificationGiven(
                    new Label("p", type),
                    ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("f", type),
                    ImmutableList.Create(
                        new PathCondition(
                            ImmutableList<Role>.Empty,
                            "p",
                            ImmutableList<Role>.Empty)),
                    ImmutableList<ExistentialCondition>.Empty)),
            new SimpleProjection("f", projected));
    }

    private static FactGraph GraphOf(params object[] facts)
    {
        var collector = new Collector(SerializerCache.Empty, new ConditionalWeakTable<object, FactGraph>());
        foreach (var fact in facts)
        {
            collector.Serialize(fact);
        }
        return collector.Graph;
    }

    private static FactReference ReferenceOf(object fact)
    {
        var collector = new Collector(SerializerCache.Empty, new ConditionalWeakTable<object, FactGraph>());
        return collector.Serialize(fact);
    }

    /// <summary>The facts one listener was notified of.</summary>
    private class DeliveredFacts
    {
        private readonly List<FactReference> references = new List<FactReference>();
        private int calls;

        public int Calls => calls;

        public IEnumerable<FactReference> References
        {
            get
            {
                lock (references)
                {
                    return references.ToArray();
                }
            }
        }

        public Task OnResult(ImmutableList<Product> products, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            lock (references)
            {
                references.AddRange(products.Select(product => product.GetFactReference("f")));
            }
            return Task.CompletedTask;
        }
    }
}
