using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Store.SQLite.Test.Fakes;
using Jinaga.Store.SQLite.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test.Observers;

/// <summary>
/// A watch on a specification that declares a label inside an existential condition and then
/// reuses it as an unknown of its own, against the SQLite store. This is jinaga-spec's
/// <c>reused-label</c> vector, and the C# query DSL renames the second declaration, so only a
/// specification built from <see cref="Specification"/> objects reaches the shape.
/// </summary>
public class WatchReusedLabelTest
{
    private readonly ITestOutputHelper output;

    public WatchReusedLabelTest(ITestOutputHelper output)
    {
        this.output = output;
    }

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
        var path = DatabasePath();
        var store = new SQLiteStore(path, NullLoggerFactory.Instance);
        var factManager = CreateFactManager(store);
        try
        {
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

            await observer.Loaded;
            rows.Count.Should().Be(1);

            await Save(factManager, new ChoreDone(chore, chore));

            rows.Count.Should().Be(0);
            rows.Removals.Should().Be(1);

            observer.Stop();
        }
        finally
        {
            await factManager.Unload();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static string DatabasePath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JinagaSQLiteTest",
            $"WatchReusedLabelTest-{Guid.NewGuid():N}.db");
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        return path;
    }

    private FactManager CreateFactManager(IStore store)
    {
        var loggerFactory = NullLoggerFactory.Instance;
        var networkManager = new NetworkManager(
            new FakeNetwork(output),
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
