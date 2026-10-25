using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Observers;
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
/// What a save costs the observer. Deciding a candidate reads the specification for that one
/// row, so absorbing a save grows with the candidates the save produces and not with the rows
/// the watch already holds.
///
/// This is the property a re-check of the whole specification would lose. Both watches here
/// run the same specification over a different number of rows, so a read that answered the
/// question by re-reading the result would cost a hundred times more in one than in the other.
/// The counts are rows read from the store, which is deterministic; a timing would not be.
/// </summary>
public class CandidateRecheckCostTest
{
    private const int FewRows = 10;
    private const int ManyRows = 1000;

    /// <summary>
    /// The accounts of a tenant that have a mirror nobody has retired. A one-level
    /// specification, so the rows the watch holds are exactly the successors of the given
    /// that a forward walk would enumerate.
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

        public CountingReadStore(IStore inner)
        {
            this.inner = inner;
        }

        public int RowsRead => rowsRead;

        public async Task<ImmutableList<Product>> Read(FactReferenceTuple givenTuple, Projections.Specification specification, CancellationToken cancellationToken)
        {
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
