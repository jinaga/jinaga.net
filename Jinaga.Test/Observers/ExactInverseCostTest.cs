using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Observers;
using Jinaga.Pipelines;
using Jinaga.Products;
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
/// What a save costs the observer. The rows of an exact inverse are the changes themselves,
/// so absorbing a save costs one read per inverse pinned on the saved fact's type and nothing
/// per row the watch already holds.
///
/// This is the property a re-check of the whole specification would lose. Both watches here
/// run the same specification over a different number of rows, so a read that answered the
/// question by re-reading the result would cost a hundred times more in one than in the other.
/// The counts are rows read from the store, which is deterministic; a timing would not be.
/// </summary>
public class ExactInverseCostTest
{
    private const int FewRows = 10;
    private const int ManyRows = 1000;

    /// <summary>
    /// Saves of the given's type that are not the given. Enough of them that a read per save
    /// is a cost nobody could mistake for noise.
    /// </summary>
    private const int UnrelatedGivens = 200;

    /// <summary>
    /// The accounts of a tenant that have a mirror nobody has retired. A one-level
    /// specification, so the rows the watch holds are exactly the successors of the given
    /// that a forward walk would enumerate, and the projection declares no collection, so
    /// the observer registers the inverses of this one specification.
    /// </summary>
    private static readonly Specification<Tenant, Account> AccountsWithACurrentMirror =
        Given<Tenant>.Match((tenant, facts) =>
            from account in facts.OfType<Account>()
            where account.tenant == tenant
            where facts.Any<Mirror>(mirror =>
                mirror.account == account &&
                !facts.Any<MirrorRetired>(retired => retired.mirror == mirror))
            select account
        );

    [Fact]
    public async Task Save_AddingOneRow_ReadsTheSameCountWhateverTheWatchHolds()
    {
        var store = new CountingReadStore(new MemoryStore());
        var factManager = CreateFactManager(store);

        var fewPending = await GivenATenantHolding(factManager, "few", FewRows);
        var manyPending = await GivenATenantHolding(factManager, "many", ManyRows);

        var few = await StartWatch(factManager, fewPending.Tenant, FewRows);
        var many = await StartWatch(factManager, manyPending.Tenant, ManyRows);

        // One fact gives the pending account a current mirror, which adds one row.
        var costOfTheFewRowWatch = await CostOfSaving(
            factManager, store, new Mirror(fewPending.Account, "east"));
        var costOfTheManyRowWatch = await CostOfSaving(
            factManager, store, new Mirror(manyPending.Account, "east"));

        costOfTheManyRowWatch.Should().Be(costOfTheFewRowWatch);

        // Both saves were really absorbed, so the equality above is not two zeroes. The
        // hundredfold difference in what the watches hold is what a re-read would have cost.
        costOfTheFewRowWatch.Should().BePositive();
        few.Rows.Count.Should().Be(FewRows + 1);
        many.Rows.Count.Should().Be(ManyRows + 1);

        few.Observer.Stop();
        many.Observer.Stop();
    }

    [Fact]
    public async Task Save_AFactOfTheGivensTypeThatIsNotTheGiven_ReadsNothing()
    {
        var store = new CountingReadStore(new MemoryStore());
        var factManager = CreateFactManager(store);

        var pending = await GivenATenantHolding(factManager, "many", ManyRows);
        var many = await StartWatch(factManager, pending.Tenant, ManyRows);

        // A row reached only by walking a given exists already when that given is saved, so
        // the observer reads the whole specification when its own given arrives. Which fact
        // arrived is a question about references, and answering it with a read charges every
        // fact of the given's type for the one that matters.
        var readsBefore = store.Reads;
        var rowsBefore = store.RowsRead;
        for (int index = 0; index < UnrelatedGivens; index++)
        {
            await Save(factManager, new Tenant($"unrelated-{index}"));
        }

        (store.Reads - readsBefore).Should().Be(0);
        (store.RowsRead - rowsBefore).Should().Be(0);
        many.Rows.Count.Should().Be(ManyRows);

        many.Observer.Stop();
    }

    [Fact]
    public async Task Save_ANonGivenFact_IssuesOnlyTheExactInversesReads()
    {
        var store = new CountingReadStore(new MemoryStore());
        var factManager = CreateFactManager(store);

        var pending = await GivenATenantHolding(factManager, "few", FewRows);
        var few = await StartWatch(factManager, pending.Tenant, FewRows);

        var readsBefore = store.Reads;
        await Save(factManager, new Mirror(pending.Account, "east"));

        // One read per inverse pinned on the saved fact's type, and not one more. The read
        // that is gone is the pinned re-check the observer used to take per candidate row,
        // and the count is derived from the inverses rather than written out, so it moves
        // when the construction does.
        (store.Reads - readsBefore).Should().Be(ExactInversesPinnedOn(MirrorType));

        few.Observer.Stop();
    }

    private const string MirrorType = "Mirror.Mirror";

    /// <summary>
    /// The reads one save of a fact of <paramref name="type"/> owes this specification: the
    /// exact inverses pinned on that type, counted as the store counts them. Listeners on one
    /// inverse and one set of givens share a single read, so inverses that describe alike
    /// count once.
    /// </summary>
    private static int ExactInversesPinnedOn(string type)
    {
        var exact = InverseCandidates.ExactInverses(AccountsWithACurrentMirror);
        return exact.Adds
            .AddRange(exact.Removes)
            .Where(inverse => inverse.Givens[inverse.Givens.Count - 1].Label.Type == type)
            .Select(inverse => inverse.ToDescriptiveString())
            .Distinct()
            .Count();
    }

    /// <summary>
    /// The rows the store read while absorbing one save, which is the save's whole cost to
    /// every observer registered on it.
    /// </summary>
    private static async Task<int> CostOfSaving(FactManager factManager, CountingReadStore store, object fact)
    {
        var before = store.RowsRead;
        await Save(factManager, fact);
        return store.RowsRead - before;
    }

    /// <summary>
    /// A tenant with <paramref name="rows"/> accounts that each have a current mirror, and one
    /// more account that has no mirror yet, so one later fact adds exactly one row.
    /// </summary>
    private static async Task<(Tenant Tenant, Account Account)> GivenATenantHolding(
        FactManager factManager, string identifier, int rows)
    {
        var tenant = new Tenant(identifier);
        await Save(factManager, tenant);
        for (int index = 0; index < rows; index++)
        {
            var account = new Account(tenant, $"account-{index}");
            await Save(factManager, account);
            await Save(factManager, new Mirror(account, "east"));
        }
        var pending = new Account(tenant, "pending");
        await Save(factManager, pending);
        return (tenant, pending);
    }

    private static async Task<(IObserver Observer, ObservedRows Rows)> StartWatch(
        FactManager factManager, Tenant tenant, int expected)
    {
        var rows = new ObservedRows();
        var reference = factManager.Serialize(tenant).Last;
        var observer = factManager.StartObserverLocal(
            FactReferenceTuple.Empty.Add(
                AccountsWithACurrentMirror.Givens[0].Label.Name, reference),
            AccountsWithACurrentMirror,
            rows.OnAdded);
        await observer.Loaded;
        rows.Count.Should().Be(expected);
        return (observer, rows);
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

    /// <summary>Counts the rows one watch holds.</summary>
    private class ObservedRows
    {
        private int count;

        public int Count => count;

        public Task<Func<Task>> OnAdded(object projection)
        {
            Interlocked.Increment(ref count);
            Func<Task> removal = () =>
            {
                Interlocked.Decrement(ref count);
                return Task.CompletedTask;
            };
            return Task.FromResult(removal);
        }
    }

    /// <summary>
    /// A store that counts the rows it returns. Rows rather than reads, because the question
    /// is how much of the result each read touches.
    /// </summary>
    private class CountingReadStore : IStore
    {
        private readonly IStore inner;
        private int rowsRead;
        private int reads;

        public CountingReadStore(IStore inner)
        {
            this.inner = inner;
        }

        public int RowsRead => rowsRead;

        /// <summary>
        /// The reads themselves, which a read that answers with nothing still costs. Rows
        /// alone cannot tell a read that was never issued from one that came back empty.
        /// </summary>
        public int Reads => reads;

        public async Task<ImmutableList<Product>> Read(FactReferenceTuple givenTuple, Projections.Specification specification, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref reads);
            var products = await inner.Read(givenTuple, specification, cancellationToken);
            Interlocked.Add(ref rowsRead, products.Count);
            return products;
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

        public Task Purge(ImmutableList<Projections.Specification> purgeConditions) =>
            inner.Purge(purgeConditions);

        public Task PurgeDescendants(FactReference purgeRoot, ImmutableList<FactReference> triggers) =>
            inner.PurgeDescendants(purgeRoot, triggers);
    }
}
