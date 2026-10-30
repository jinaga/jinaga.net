using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Observers;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Storage;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Jinaga.Test.Observers;

/// <summary>
/// What an inverse listener observes. An inverse's givens are the watch's own
/// givens followed by the pin label, and the read binds all of them, so a
/// listener receives only the rows of its own watch and nothing filters a
/// result afterwards.
///
/// <see cref="ObservableSource.AddSpecificationListener"/> cannot carry these:
/// it takes a specification of exactly one given and reaches that given from
/// the saved fact alone, which a specification whose given is joined only
/// inside an existential condition has no inverse of that shape for.
/// </summary>
public class InverseListenerTest
{
    private const string AlicePublicKey = "---FAKE ALICE PUBLIC KEY---";

    /// <summary>
    /// The messages of a channel that a user has not read. Its first given is
    /// joined only from inside the negated condition, which is the shape
    /// <see cref="Inverter"/> refuses.
    /// </summary>
    private static readonly Specification<User, Channel, Message> UnreadMessages =
        Given<User, Channel>.Match((p1, p2, facts) =>
            from m in facts.OfType<Message>()
            where m.channel == p2
            where !facts.Any<Read>(r => r.message == m && r.user == p1)
            select m
        );

    /// <summary>
    /// An inverse of the messages of a channel, pinned on the message. The
    /// shape a candidate inverse has: the watch's given <c>p2</c>, then the pin
    /// label, and a match that starts from the pinned fact.
    /// </summary>
    private static readonly Projections.Specification MessageInChannelInverse =
        new Projections.Specification(
            ImmutableList.Create(
                new SpecificationGiven(new Label("p2", "Chat.Channel"), ImmutableList<ExistentialCondition>.Empty),
                new SpecificationGiven(new Label(InverseCandidates.PinLabel, "Chat.Message"), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("m", "Chat.Message"),
                    ImmutableList.Create(
                        new PathCondition(ImmutableList<Role>.Empty, InverseCandidates.PinLabel, ImmutableList<Role>.Empty),
                        new PathCondition(ImmutableList.Create(new Role("channel", "Chat.Channel")), "p2", ImmutableList<Role>.Empty)),
                    ImmutableList<ExistentialCondition>.Empty)),
            new SimpleProjection("m", typeof(Message)));

    /// <summary>
    /// An inverse that reaches a predecessor of the watch's given rather than of the
    /// pinned fact. Its one path condition walks a predecessor, so the specification
    /// could run on a graph, and the graph of the saved fact holds neither the given
    /// nor the channel it names.
    /// </summary>
    private static readonly Projections.Specification ChannelOfTheGivenMessageInverse =
        new Projections.Specification(
            ImmutableList.Create(
                new SpecificationGiven(new Label("p2", "Chat.Message"), ImmutableList<ExistentialCondition>.Empty),
                new SpecificationGiven(new Label(InverseCandidates.PinLabel, "Chat.Channel"), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("c", "Chat.Channel"),
                    ImmutableList.Create(
                        new PathCondition(
                            ImmutableList<Role>.Empty,
                            "p2",
                            ImmutableList.Create(new Role("channel", "Chat.Channel")))),
                    ImmutableList<ExistentialCondition>.Empty)),
            new SimpleProjection("c", typeof(Channel)));

    [Fact]
    public void Inverter_WithAGivenJoinedOnlyInsideANegatedCondition_Refuses()
    {
        // Why the inverse listener exists. Nothing registered through
        // AddSpecificationListener can observe this specification, because the
        // inverses it would need bind a given the saved fact cannot reach.
        var act = () => Inverter.InvertSpecification(UnreadMessages);

        act.Should().Throw<ArgumentException>().WithMessage("Malformed specification.");
    }

    [Fact]
    public async Task AddInverseListener_SavingTheReadThatFalsifiesTheCondition_DeliversTheMessage()
    {
        var store = new MemoryStore();
        var factManager = CreateFactManager(store);

        var alice = new User(AlicePublicKey);
        var general = new Channel("general");
        var message = new Message(general, "Where is everybody?");
        var aliceReference = await Save(factManager, alice);
        var generalReference = await Save(factManager, general);
        var messageReference = await Save(factManager, message);

        var givens = FactReferenceTuple.Empty
            .Add("p1", aliceReference)
            .Add("p2", generalReference);
        var delivered = new List<Product>();
        foreach (var inverse in InverseCandidates.CandidateInverses(UnreadMessages))
        {
            factManager.AddInverseListener(
                inverse,
                givens,
                () => 0,
                (products, read, cancellationToken) =>
                {
                    lock (delivered)
                    {
                        delivered.AddRange(products);
                    }
                    return Task.CompletedTask;
                },
                read => { });
        }

        await Save(factManager, new Read(message, alice));

        delivered.SelectMany(product => product.GetFactReferences())
            .Should().Contain(messageReference);
    }

    [Fact]
    public async Task AddInverseListener_WithAnInverseThatCouldRunOnAGraph_ReadsTheStoreInstead()
    {
        // The input only guards the store read while it is a specification the
        // graph shortcut would otherwise take.
        ChannelOfTheGivenMessageInverse.CanRunOnGraph.Should().BeTrue();

        var store = new MemoryStore();
        var factManager = CreateFactManager(store);

        var general = new Channel("general");
        var generalReference = await Save(factManager, general);
        var messageReference = await Save(factManager, new Message(general, "Where is everybody?"));

        var delivered = new DeliveredProducts();
        factManager.AddInverseListener(
            ChannelOfTheGivenMessageInverse,
            FactReferenceTuple.Empty.Add("p2", messageReference),
            delivered.BeginRead,
            delivered.OnResult,
            delivered.EndRead);

        // A channel with no predecessors, so the saved batch's graph holds that one
        // fact. Only a read on the store can reach the given message's channel.
        await Save(factManager, new Channel("lounge"));

        delivered.Products.SelectMany(product => product.GetFactReferences())
            .Should().Contain(generalReference);
    }

    [Fact]
    public async Task AddInverseListener_WithTwoGivens_ReadsOncePerGivensAndDeliversOnlyItsOwnRows()
    {
        var store = new CountingReadStore(new MemoryStore());
        var factManager = CreateFactManager(store);

        var general = new Channel("general");
        var lounge = new Channel("lounge");
        var generalReference = await Save(factManager, general);
        var loungeReference = await Save(factManager, lounge);

        var generalGivens = FactReferenceTuple.Empty.Add("p2", generalReference);
        var loungeGivens = FactReferenceTuple.Empty.Add("p2", loungeReference);
        var firstOnGeneral = new DeliveredProducts();
        var secondOnGeneral = new DeliveredProducts();
        var onLounge = new DeliveredProducts();
        factManager.AddInverseListener(
            MessageInChannelInverse, generalGivens, firstOnGeneral.BeginRead, firstOnGeneral.OnResult, firstOnGeneral.EndRead);
        factManager.AddInverseListener(
            MessageInChannelInverse, generalGivens, secondOnGeneral.BeginRead, secondOnGeneral.OnResult, secondOnGeneral.EndRead);
        factManager.AddInverseListener(
            MessageInChannelInverse, loungeGivens, onLounge.BeginRead, onLounge.OnResult, onLounge.EndRead);

        var readsBeforeTheSave = store.Reads;
        var message = new Message(general, "Where is everybody?");
        var messageReference = await Save(factManager, message);

        // Two distinct givens, so two reads, although three listeners share them.
        (store.Reads - readsBeforeTheSave).Should().Be(2);
        firstOnGeneral.Products.SelectMany(product => product.GetFactReferences())
            .Should().Contain(messageReference);
        secondOnGeneral.Products.SelectMany(product => product.GetFactReferences())
            .Should().Contain(messageReference);
        onLounge.Products.Should().BeEmpty();

        // The hook's contract, for each of the two listeners that share one read: the number
        // is taken, the handler receives that same number, and it is ended afterwards.
        firstOnGeneral.Order.Should().Equal("begin 1", "result 1", "end 1");
        secondOnGeneral.Order.Should().Equal("begin 1", "result 1", "end 1");

        // The lounge listener's read came back with no rows of its own, and it still ends its
        // number. A number left pending holds every withdrawal older than it, waiting on a
        // read that nothing would ever finish.
        onLounge.Order.Should().Equal("begin 1", "result 1", "end 1");
        onLounge.Ended.Should().Be(1);
    }

    [Fact]
    public async Task AddInverseListener_SavingAFactOfAnotherType_ReadsNothingAndNotifiesNobody()
    {
        var store = new CountingReadStore(new MemoryStore());
        var factManager = CreateFactManager(store);

        var generalReference = await Save(factManager, new Channel("general"));
        var givens = FactReferenceTuple.Empty.Add("p2", generalReference);
        var delivered = new DeliveredProducts();
        factManager.AddInverseListener(
            MessageInChannelInverse, givens, delivered.BeginRead, delivered.OnResult, delivered.EndRead);

        var readsBeforeTheSave = store.Reads;
        await Save(factManager, new Channel("lounge"));

        (store.Reads - readsBeforeTheSave).Should().Be(0);
        delivered.Calls.Should().Be(0);

        // No read was issued, so no number was taken either. A listener whose inverse is
        // pinned on another type is never reached, rather than reached with nothing.
        delivered.Order.Should().BeEmpty();
        delivered.Ended.Should().Be(0);
    }

    [Fact]
    public async Task RemoveInverseListener_ThenSavingAMatchingFact_CallsNobody()
    {
        var store = new CountingReadStore(new MemoryStore());
        var factManager = CreateFactManager(store);

        var general = new Channel("general");
        var generalReference = await Save(factManager, general);
        var givens = FactReferenceTuple.Empty.Add("p2", generalReference);
        var delivered = new DeliveredProducts();
        var listener = factManager.AddInverseListener(
            MessageInChannelInverse, givens, delivered.BeginRead, delivered.OnResult, delivered.EndRead);

        factManager.RemoveInverseListener(listener);

        var readsBeforeTheSave = store.Reads;
        await Save(factManager, new Message(general, "Where is everybody?"));

        delivered.Calls.Should().Be(0);
        (store.Reads - readsBeforeTheSave).Should().Be(0);
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

    private static async Task<FactReference> Save(FactManager factManager, object fact)
    {
        var graph = factManager.Serialize(fact);
        await factManager.SaveLocal(graph, default);
        return graph.Last;
    }

    /// <summary>
    /// Stands in for one observer's side of an inverse listener, and records the order its
    /// three members were called in. A read is numbered here exactly as an observer numbers
    /// one, so a test can ask whether the number was taken before the store was read.
    /// </summary>
    private class DeliveredProducts
    {
        private readonly List<Product> products = new List<Product>();
        private readonly List<string> order = new List<string>();
        private long lastRead;
        private int calls;
        private int ended;

        public int Calls => calls;

        /// <summary>The reads this listener was asked to end, which a read that came back with nothing still owes.</summary>
        public int Ended => ended;

        public IEnumerable<Product> Products
        {
            get
            {
                lock (products)
                {
                    return products.ToArray();
                }
            }
        }

        /// <summary>What happened, in order: a number taken, a result applied, a number ended.</summary>
        public IEnumerable<string> Order
        {
            get
            {
                lock (order)
                {
                    return order.ToArray();
                }
            }
        }

        public long BeginRead()
        {
            var read = Interlocked.Increment(ref lastRead);
            Record($"begin {read}");
            return read;
        }

        public Task OnResult(ImmutableList<Product> results, long read, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            Record($"result {read}");
            lock (products)
            {
                products.AddRange(results);
            }
            return Task.CompletedTask;
        }

        public void EndRead(long read)
        {
            Interlocked.Increment(ref ended);
            Record($"end {read}");
        }

        private void Record(string entry)
        {
            lock (order)
            {
                order.Add(entry);
            }
        }
    }

    /// <summary>
    /// A store that counts the reads that pass through it, so that a test can
    /// tell how many reads a save produced.
    /// </summary>
    private class CountingReadStore : IStore
    {
        private readonly IStore inner;
        private int reads;

        public CountingReadStore(IStore inner)
        {
            this.inner = inner;
        }

        public int Reads => reads;

        public Task<ImmutableList<Product>> Read(FactReferenceTuple givenTuple, Projections.Specification specification, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref reads);
            return inner.Read(givenTuple, specification, cancellationToken);
        }

        public bool IsPersistent => inner.IsPersistent;

        public Task<ImmutableList<Fact>> Save(FactGraph graph, bool queue, CancellationToken cancellationToken) =>
            inner.Save(graph, queue, cancellationToken);

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

        public Task Purge(ImmutableList<Projections.Specification> purgeConditions) => inner.Purge(purgeConditions);

        public Task PurgeDescendants(FactReference purgeRoot, ImmutableList<FactReference> triggers) =>
            inner.PurgeDescendants(purgeRoot, triggers);
    }
}
