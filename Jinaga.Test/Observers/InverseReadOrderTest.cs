using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;

namespace Jinaga.Test.Observers;

/// <summary>
/// When an inverse read is numbered. The rows of an exact inverse are the changes
/// themselves, so nothing reads again to decide them, and the inverse read is the read
/// that orders a row's decisions. It has to be numbered, and pending, before it touches
/// the store.
///
/// A number taken when the handler runs instead would leave the read unnumbered across
/// the store read and across every handler awaited ahead of it. A removal that lands in
/// that gap finds no older read pending, so it records no withdrawal, and the stale add
/// behind it claims a row the store no longer holds.
/// </summary>
public class InverseReadOrderTest
{
    private readonly ITestOutputHelper output;

    public InverseReadOrderTest(ITestOutputHelper output)
    {
        this.output = output;
    }

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
    public async Task Watch_AnAddReadHeldOpenPastANewerRemoval_NeverDeliversTheRow()
    {
        var inner = new MemoryStore();
        var store = new GatedReadStore(inner);
        var j = GivenJinagaClient(store);

        var tenant = await j.Fact(new Tenant("contoso"));
        var account = await j.Fact(new Account(tenant, "alice"));

        var added = new List<Account>();
        var removals = 0;
        var watch = j.Watch(AccountsWithACurrentMirror, tenant, (Account a) =>
        {
            lock (added)
            {
                added.Add(a);
            }
            Func<Task> removal = () =>
            {
                Interlocked.Increment(ref removals);
                lock (added)
                {
                    added.Remove(a);
                }
                return Task.CompletedTask;
            };
            return Task.FromResult(removal);
        });
        await watch.Loaded;

        // The account has no mirror yet, so the initial read holds nothing and every
        // delivery below comes from an inverse.
        added.Should().BeEmpty();

        var mirror = new Mirror(account, "west");
        store.GateReadsPinnedOn(j.Hash(mirror));

        // The add read for the mirror runs against a store with no retirement in it, and
        // then stops inside the store with its rows already taken.
        var adding = j.Local.Fact(mirror);
        await store.GatedReadTaken;

        // The retirement is saved and decided while that add is still in flight. Its read
        // is numbered after the add's, so it is the newer decision.
        await j.Local.Fact(new MirrorRetired(mirror));

        store.ReleaseGate();
        await adding;

        // The add was numbered before the removal, so it is the stale report of the two
        // and the row it carries is never delivered.
        added.Should().BeEmpty();
        removals.Should().Be(0);
        (await j.Query(AccountsWithACurrentMirror, tenant)).Should().BeEmpty();

        // The gate really held an add read open, so the assertions above are not three
        // statements about a read that never happened.
        store.GatedRowsRead.Should().BePositive();

        watch.Stop();
    }

    private JinagaClient GivenJinagaClient(IStore store)
    {
        return new JinagaClient(
            store,
            new FakeNetwork(output),
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            new JinagaClientOptions());
    }

    /// <summary>
    /// A store that holds one inverse read open after it has taken its rows. The read is
    /// chosen by the fact the inverse is pinned on, and only a read that came back with
    /// rows is held, so the test cannot pass by gating a read that reported no change.
    /// </summary>
    private class GatedReadStore : IStore
    {
        private readonly IStore inner;
        private readonly TaskCompletionSource<bool> gateTaken = new TaskCompletionSource<bool>();
        private readonly TaskCompletionSource<bool> gateReleased = new TaskCompletionSource<bool>();
        private string? pinnedHash;
        private int gatedRowsRead;

        public GatedReadStore(IStore inner)
        {
            this.inner = inner;
        }

        /// <summary>Completes once a read pinned on the chosen fact has taken its rows.</summary>
        public Task GatedReadTaken => gateTaken.Task;

        /// <summary>The rows the held read came back with.</summary>
        public int GatedRowsRead => gatedRowsRead;

        public void GateReadsPinnedOn(string hash)
        {
            pinnedHash = hash;
        }

        public void ReleaseGate()
        {
            gateReleased.TrySetResult(true);
        }

        public async Task<ImmutableList<Product>> Read(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            var products = await inner.Read(givenTuple, specification, cancellationToken);
            if (IsGated(givenTuple) && products.Any() && !gateTaken.Task.IsCompleted)
            {
                Interlocked.Add(ref gatedRowsRead, products.Count);
                gateTaken.SetResult(true);
                await gateReleased.Task;
            }
            return products;
        }

        private bool IsGated(FactReferenceTuple givenTuple)
        {
            return pinnedHash != null &&
                givenTuple.Names.Contains(InverseCandidates.PinLabel) &&
                givenTuple.Get(InverseCandidates.PinLabel).Hash == pinnedHash;
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

        public Task Purge(ImmutableList<Specification> purgeConditions) =>
            inner.Purge(purgeConditions);

        public Task PurgeDescendants(FactReference purgeRoot, ImmutableList<FactReference> triggers) =>
            inner.PurgeDescendants(purgeRoot, triggers);
    }
}
