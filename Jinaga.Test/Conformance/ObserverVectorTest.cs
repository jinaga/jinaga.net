using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Storage;
using Jinaga.UnitTest;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jinaga.Test.Conformance;

/// <summary>
/// Every inverse vector of jinaga-spec, run through the observer rather than through the
/// inverses alone. A watch is started on the vector's specification and its givens, the
/// vector's arrival is saved as one batch, and what the watch then holds is compared with a
/// read of the same specification over the same store.
///
/// <see cref="InverseVectorTest"/> asks whether the inverses find the candidates. This asks
/// the question the observer answers: whether a candidate re-checked against the store leaves
/// the watch agreeing with a read. The two can come apart, because an inverse that finds every
/// candidate still says nothing about which of them changed membership.
/// </summary>
public class ObserverVectorTest
{
    public static IEnumerable<object[]> Vectors => ConformanceVector.Names("inverse");

    [Theory]
    [MemberData(nameof(Vectors))]
    public async Task TheWatchHoldsWhatAReadHolds(string name)
    {
        var vector = ConformanceVector.Load("inverse", name);
        var rowLabels = UnknownLabels(vector);
        var specification = ConformanceVector.ToSpecification(
            vector.Json.GetProperty("specification"), RowProjection(rowLabels));
        var references = References(vector);

        var store = new MemoryStore();
        PlaceGraph(store, vector, references);
        var factManager = CreateFactManager(store);
        var givenTuple = GivenTuple(vector, references);

        var observed = new ObservedRows();
        var observer = factManager.StartObserverLocal(givenTuple, specification, observed.OnAdded);
        await observer.Loaded;

        await factManager.SaveLocal(ArrivalGraph(vector, references), default);

        var read = await store.Read(givenTuple, specification, CancellationToken.None);
        observed.Rows.Should().Equal(Rows(read, rowLabels));

        observer.Stop();
    }

    /// <summary>
    /// The rows of a read, keyed by the same labels the watch keys its rows by, in a fixed
    /// order and without repetition.
    /// </summary>
    /// <remarks>
    /// A read answers with a list and a watch holds a set. The two differ where a fact names
    /// one predecessor twice, which <c>MemoryStore</c> walks as two edges and answers with the
    /// same tuple twice. The watch delivers such a row once, and that is deliberate: a second
    /// notification of a row the caller already holds is a duplicate it never asked for.
    /// </remarks>
    private static ImmutableList<string> Rows(ImmutableList<Product> products, ImmutableList<string> rowLabels) =>
        products
            .Select(product => Row(rowLabels.Select(label => product.GetFactReference(label).Hash)))
            .Distinct()
            .OrderBy(row => row, StringComparer.Ordinal)
            .ToImmutableList();

    /// <summary>
    /// A projection of the labels that identify a row as hashes.
    /// </summary>
    /// <remarks>
    /// A vector's own projection names facts of types no model here declares, and may name one
    /// component twice, which <see cref="CompoundProjection"/> cannot hold. A hash is what the
    /// vector states its rows in, and reading one needs nothing of the fact but its reference.
    /// The labels are the specification's own unknowns rather than the vector's
    /// <c>rowLabels</c>, which are what its projection reads and do not always identify a row:
    /// several of the random vectors project one label of a specification of several unknowns,
    /// and keying rows by it would compare a watch's rows two at a time.
    /// </remarks>
    private static Projection RowProjection(ImmutableList<string> rowLabels)
    {
        var type = rowLabels.Count switch
        {
            1 => typeof(Row1),
            2 => typeof(Row2),
            3 => typeof(Row3),
            4 => typeof(Row4),
            var count => throw new NotSupportedException(
                $"A specification of {count} unknowns needs a row type of that many components.")
        };
        var components = ImmutableDictionary<string, Projection>.Empty;
        for (int index = 0; index < rowLabels.Count; index++)
        {
            components = components.Add(
                $"c{index}", new HashProjection(rowLabels[index], typeof(object)));
        }
        return new CompoundProjection(components, type);
    }

    private record Row1(string c0);

    private record Row2(string c0, string c1);

    private record Row3(string c0, string c1, string c2);

    private record Row4(string c0, string c1, string c2, string c3);

    /// <summary>
    /// The reference the reference implementation gives each of the vector's facts. A vector
    /// states these itself, because its graph may name a predecessor content addressing cannot
    /// reach: a fact that arrives later, or a cycle.
    /// </summary>
    private static ImmutableDictionary<int, FactReference> References(ConformanceVector vector) =>
        Graph(vector).Concat(Arrivals(vector)).ToImmutableDictionary(
            IdOf,
            fact => new FactReference(fact.GetProperty("type").GetString()!, "h" + IdOf(fact)));

    private static void PlaceGraph(
        MemoryStore store, ConformanceVector vector, ImmutableDictionary<int, FactReference> references)
    {
        store.PlaceGraphByReference(Graph(vector)
            .Select(fact => (references[IdOf(fact)], PredecessorsOf(fact, references))));
    }

    /// <summary>
    /// The facts that arrive, as one graph. A batch arrives together, so it is saved together
    /// and the observer decides every candidate of the whole batch.
    /// </summary>
    /// <remarks>
    /// The graph is built by adding each fact outright rather than through
    /// <see cref="FactGraphBuilder"/>, which holds back a fact whose predecessors it does not
    /// carry: an arrival's predecessors are usually in the store rather than in the batch.
    /// Within the batch they are ordered so that a fact follows the arrivals it names, which
    /// is the order the store adds them in.
    /// </remarks>
    private static FactGraph ArrivalGraph(
        ConformanceVector vector, ImmutableDictionary<int, FactReference> references)
    {
        var graph = FactGraph.Empty;
        foreach (var fact in PredecessorsFirst(Arrivals(vector).ToImmutableList()))
        {
            graph = graph.Add(new FactEnvelope(
                Fact.CreateByReference(
                    references[IdOf(fact)],
                    ImmutableList<Field>.Empty,
                    PredecessorsOf(fact, references)),
                ImmutableList<FactSignature>.Empty));
        }
        return graph;
    }

    /// <summary>
    /// The arrivals ordered so that each one follows those of them it names as predecessors.
    /// A vector states its batch in no particular order.
    /// </summary>
    private static ImmutableList<JsonElement> PredecessorsFirst(ImmutableList<JsonElement> arrivals)
    {
        var byId = arrivals.ToImmutableDictionary(IdOf);
        var ordered = ImmutableList<JsonElement>.Empty;
        var placed = new HashSet<int>();
        void Place(JsonElement fact, ImmutableList<int> visiting)
        {
            var id = IdOf(fact);
            if (placed.Contains(id) || visiting.Contains(id))
            {
                return;
            }
            foreach (var predecessor in fact.GetProperty("predecessors").EnumerateArray())
            {
                if (byId.TryGetValue(IdOf(predecessor), out var earlier))
                {
                    Place(earlier, visiting.Add(id));
                }
            }
            if (placed.Add(id))
            {
                ordered = ordered.Add(fact);
            }
        }
        foreach (var fact in arrivals)
        {
            Place(fact, ImmutableList<int>.Empty);
        }
        return ordered;
    }

    /// <summary>
    /// A fact's predecessors. A fact lists one entry per predecessor, and a role may appear
    /// more than once.
    /// </summary>
    private static ImmutableList<Predecessor> PredecessorsOf(
        JsonElement fact, ImmutableDictionary<int, FactReference> references) =>
        fact.GetProperty("predecessors").EnumerateArray()
            .Select(predecessor => (Predecessor)new PredecessorSingle(
                predecessor.GetProperty("role").GetString()!,
                references[IdOf(predecessor)]))
            .ToImmutableList();

    private static FactReferenceTuple GivenTuple(
        ConformanceVector vector, ImmutableDictionary<int, FactReference> references)
    {
        var givens = vector.Json.GetProperty("givens");
        return vector.Json.GetProperty("specification").GetProperty("given").EnumerateArray()
            .Aggregate(
                FactReferenceTuple.Empty,
                (tuple, given) =>
                {
                    var name = given.GetProperty("label").GetProperty("name").GetString()!;
                    return tuple.Add(name, references[givens.GetProperty(name).GetInt32()]);
                });
    }

    private static IEnumerable<JsonElement> Graph(ConformanceVector vector) =>
        vector.Json.GetProperty("graph").EnumerateArray();

    /// <summary>The facts that arrive: a batch, saved together, or a single fact.</summary>
    private static IEnumerable<JsonElement> Arrivals(ConformanceVector vector) =>
        vector.Json.TryGetProperty("batch", out var batch)
            ? batch.EnumerateArray()
            : ImmutableList.Create(vector.Json.GetProperty("fact"));

    /// <summary>
    /// The labels that identify a row of the vector's specification: its own unknowns, each
    /// once. The givens are the same for every row of one watch, so the unknowns settle a
    /// row's identity within it.
    /// </summary>
    private static ImmutableList<string> UnknownLabels(ConformanceVector vector) =>
        vector.Json.GetProperty("specification").GetProperty("matches").EnumerateArray()
            .Select(match => match.GetProperty("unknown").GetProperty("name").GetString()!)
            .Distinct()
            .ToImmutableList();

    private static int IdOf(JsonElement json) => json.GetProperty("id").GetInt32();

    private static string Row(IEnumerable<string> hashes) => string.Join(",", hashes);

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

    /// <summary>The rows one watch holds, keyed as the vector keys them.</summary>
    private class ObservedRows
    {
        private readonly List<string> rows = new List<string>();

        public ImmutableList<string> Rows
        {
            get
            {
                lock (rows)
                {
                    return rows.OrderBy(row => row, StringComparer.Ordinal).ToImmutableList();
                }
            }
        }

        public Task<Func<Task>> OnAdded(object projection)
        {
            var row = KeyOf(projection);
            lock (rows)
            {
                rows.Add(row);
            }
            Func<Task> removal = () =>
            {
                lock (rows)
                {
                    rows.Remove(row);
                }
                return Task.CompletedTask;
            };
            return Task.FromResult(removal);
        }

        private static string KeyOf(object projection) => projection switch
        {
            Row1 row => Row(new[] { row.c0 }),
            Row2 row => Row(new[] { row.c0, row.c1 }),
            Row3 row => Row(new[] { row.c0, row.c1, row.c2 }),
            Row4 row => Row(new[] { row.c0, row.c1, row.c2, row.c3 }),
            _ => throw new NotSupportedException($"A row of {projection.GetType().Name} has no key.")
        };
    }
}
