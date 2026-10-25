using Jinaga.DefaultImplementations;
using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Services;
using Jinaga.Storage;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Jinaga.Store.SQLite.Test;

/// <summary>
/// A read on one connection runs while a save runs on another. Whatever the
/// interleaving, the read sees a graph the store actually held, and a refused
/// save leaves the store as it found it.
/// </summary>
public class SaveAndReadIsolationTest
{
    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "SaveAndReadIsolationTest.db");

    /// <summary>
    /// The store holds a root with one A and one B, so the specification's
    /// product gives one row. A save adds a second A and a second B in one
    /// graph, after which the product gives four. A read that returns two has
    /// seen the second A without the second B, which is a state no graph the
    /// store ever held can produce.
    /// </summary>
    [Fact]
    public async Task AReadDuringASaveSeesEitherGraphAndNothingBetween()
    {
        IStore store = GivenEmptySQLiteStore();

        var specification = Given<IsolationRoot>.Match((root, facts) =>
            from a in facts.OfType<IsolationA>()
            where a.root == root
            from b in facts.OfType<IsolationB>()
            where b.root == root
            select new { a, b });
        var givenName = specification.Givens.Single().Label.Name;

        var rowCounts = new ConcurrentBag<int>();

        for (int round = 0; round < 25; round++)
        {
            var root = new IsolationRoot($"round-{round}");
            await store.Save(GraphOf(new IsolationA(root, 1)), false, default);
            await store.Save(GraphOf(new IsolationB(root, 1)), false, default);

            var givenTuple = FactReferenceTuple.Empty
                .Add(givenName, ReferenceOfFact(root));
            var secondPair = GraphOfOnly(new IsolationA(root, 2))
                .AddGraph(GraphOfOnly(new IsolationB(root, 2)));

            using var readingStopped = new CancellationTokenSource();
            var reading = Task.Run(async () =>
            {
                while (!readingStopped.IsCancellationRequested)
                {
                    var products = await store.Read(givenTuple, specification, default);
                    rowCounts.Add(products.Count);
                }
            });

            await store.Save(secondPair, false, default);
            readingStopped.Cancel();
            await reading;
        }

        rowCounts.Should().NotBeEmpty();
        rowCounts.Should().OnlyContain(count => count == 1 || count == 4);
    }

    /// <summary>
    /// The root query and the nested collection's query are separate
    /// statements. Every save adds one A and one B together, so in every
    /// committed state the store holds as many B facts as A facts, and each
    /// row of a read carries a collection whose size is the row count. A read
    /// whose two statements straddle a commit sees one more B than that.
    /// </summary>
    [Fact]
    public async Task AReadSeesOneCommittedStateAcrossItsNestedQueries()
    {
        IStore store = GivenEmptySQLiteStore();
        var client = new JinagaClient(store, new LocalNetwork(), [], NullLoggerFactory.Instance, new JinagaClientOptions());

        var root = new IsolationRoot("nested");
        var specification = Given<IsolationRoot>.Match((root, facts) =>
            from a in facts.OfType<IsolationA>()
            where a.root == root
            select new
            {
                aIndex = a.index,
                bs =
                    from b in facts.OfType<IsolationB>()
                    where b.root == root
                    select b.index
            });

        await store.Save(GraphOf(root), false, default);

        using var readingStopped = new CancellationTokenSource();
        var mismatches = new ConcurrentBag<string>();
        var reading = Task.Run(async () =>
        {
            while (!readingStopped.IsCancellationRequested)
            {
                var rows = await client.Local.Query(specification, root);
                foreach (var row in rows)
                {
                    var bCount = row.bs.Count();
                    if (bCount != rows.Count)
                    {
                        mismatches.Add($"{rows.Count} A facts against {bCount} B facts");
                    }
                }
            }
        });

        for (int index = 1; index <= 40; index++)
        {
            var pair = GraphOfOnly(new IsolationA(root, index))
                .AddGraph(GraphOfOnly(new IsolationB(root, index)));
            await store.Save(pair, false, default);
        }

        readingStopped.Cancel();
        await reading;

        mismatches.Should().BeEmpty();
    }

    /// <summary>
    /// The third fact of the graph names a predecessor that is in neither the
    /// graph nor the store, so the save is refused. The two facts in front of
    /// it are not stored either.
    /// </summary>
    [Fact]
    public async Task ARefusedSaveStoresNoneOfTheGraph()
    {
        IStore store = GivenEmptySQLiteStore();

        var root = new IsolationRoot("refused");
        var a = new IsolationA(root, 1);
        var unsavedRoot = new IsolationRoot("unsaved");
        var b = new IsolationB(unsavedRoot, 1);

        // The root, then an A, then a B whose own root is absent.
        var graph = GraphOf(a).AddGraph(GraphOfOnly(b));
        graph.FactReferences.Should().HaveCount(3);

        Func<Task> save = () => store.Save(graph, false, default);
        await save.Should().ThrowAsync<InvalidOperationException>();

        var loaded = await store.Load(
            ImmutableList.Create(ReferenceOfFact(root), ReferenceOfFact(a)),
            default);
        loaded.FactReferences.Should().BeEmpty();
    }

    private static SQLiteStore GivenEmptySQLiteStore()
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);
        return new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
    }

    private static FactGraph GraphOf(object fact)
    {
        var store = new MemoryStore();
        var loggerFactory = NullLoggerFactory.Instance;
        var networkManager = new NetworkManager(new LocalNetwork(), store, loggerFactory, (FactGraph g, ImmutableList<Fact> l, CancellationToken c) => Task.CompletedTask, new JinagaClientOptions().MaxBatchSize);
        var factManager = new FactManager(store, networkManager, [], loggerFactory, 0);
        return factManager.Serialize(fact);
    }

    /// <summary>
    /// Serializes a fact and returns a graph that holds only that fact,
    /// leaving out the predecessors that serialization put in front of it.
    /// </summary>
    private static FactGraph GraphOfOnly(object fact)
    {
        var graph = GraphOf(fact);
        return FactGraph.Empty.Add(graph.GetEnvelope(graph.Last));
    }

    private static FactReference ReferenceOfFact(object fact)
    {
        return GraphOf(fact).Last;
    }
}
