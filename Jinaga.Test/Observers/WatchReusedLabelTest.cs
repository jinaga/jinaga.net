using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Observers;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Storage;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using System.Threading;

namespace Jinaga.Test.Observers;

/// <summary>
/// A watch on a specification that declares a label inside an existential condition and then
/// reuses it as an unknown of its own, which is jinaga-spec's <c>reused-label</c> vector.
///
/// The C# query DSL renames the second declaration, so only a specification built from
/// <see cref="Specification"/> objects reaches this shape. Inverting it broke on the reused
/// label before the observer stopped inverting.
/// </summary>
public class WatchReusedLabelTest
{
    /// <summary>
    /// The chores of a tenant that no completion names, paired with the tenant's accounts.
    /// The completion is bound to <c>x</c> inside the negated condition, and the account is
    /// bound to <c>x</c> again after it.
    /// </summary>
    private static readonly Specification ChoresAndAccounts = new Specification(
        ImmutableList.Create(
            new SpecificationGiven(new Label("r", "Mirror.Tenant"), ImmutableList<ExistentialCondition>.Empty)),
        ImmutableList.Create(
            new Match(
                new Label("t", "Mirror.Chore"),
                ImmutableList.Create(new PathCondition(
                    ImmutableList.Create(new Role("tenant", "Mirror.Tenant")), "r", ImmutableList<Role>.Empty)),
                ImmutableList.Create(new ExistentialCondition(false, ImmutableList.Create(
                    new Match(
                        new Label("x", "Mirror.ChoreDone"),
                        ImmutableList.Create(new PathCondition(
                            ImmutableList.Create(new Role("chore", "Mirror.Chore")), "t", ImmutableList<Role>.Empty)),
                        ImmutableList<ExistentialCondition>.Empty))))),
            new Match(
                new Label("x", "Mirror.Account"),
                ImmutableList.Create(new PathCondition(
                    ImmutableList.Create(new Role("tenant", "Mirror.Tenant")), "r", ImmutableList<Role>.Empty)),
                ImmutableList<ExistentialCondition>.Empty)),
        new SimpleProjection("t", typeof(Chore)));

    [Fact]
    public async Task Watch_ALabelReusedAfterAnExistentialCondition_DeliversTheRemoval()
    {
        var factManager = CreateFactManager(new MemoryStore());
        var tenant = new Tenant("contoso");
        var tenantReference = await Save(factManager, tenant);
        var chore = new Chore(tenant, "sweep");
        await Save(factManager, chore);
        await Save(factManager, new Account(tenant, "alice"));

        var rows = new ObservedRows();
        var observer = factManager.StartObserverLocal(
            FactReferenceTuple.Empty.Add("r", tenantReference),
            ChoresAndAccounts,
            rows.OnAdded);

        // Inverting this specification threw on the reused label, so the read that registered
        // the listeners faulted and the watch never loaded.
        await observer.Loaded;
        rows.Count.Should().Be(1);

        await Save(factManager, new ChoreDone(chore, chore));

        rows.Count.Should().Be(0);
        rows.Removals.Should().Be(1);

        observer.Stop();
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
        return new FactManager(store, networkManager, ImmutableList<Specification>.Empty, loggerFactory, 0);
    }

    private static async Task<FactReference> Save(FactManager factManager, object fact)
    {
        var graph = factManager.Serialize(fact);
        await factManager.SaveLocal(graph, default);
        return graph.Last;
    }

    /// <summary>Counts the rows one watch holds and the removals it has been told about.</summary>
    private class ObservedRows
    {
        private int count;
        private int removals;

        public int Count => count;
        public int Removals => removals;

        public Task<Func<Task>> OnAdded(object projection)
        {
            Interlocked.Increment(ref count);
            Func<Task> removal = () =>
            {
                Interlocked.Decrement(ref count);
                Interlocked.Increment(ref removals);
                return Task.CompletedTask;
            };
            return Task.FromResult(removal);
        }
    }
}
