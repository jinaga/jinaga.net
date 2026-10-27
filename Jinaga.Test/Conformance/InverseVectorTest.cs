using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Storage;

namespace Jinaga.Test.Conformance;

/// <summary>
/// The inverse vectors of jinaga-spec: the inverses its proven <c>candidates</c> algorithm
/// builds for a specification, and the reordering of each, read from the subscription's givens
/// and each fresh fact, find exactly the rows the spec calls the candidates.
///
/// Porting that algorithm is its own change. These vectors gate it: they state what a port
/// must produce before one exists, and they check that this library reads the specifications
/// such a port will build the way the reference semantics reads them. The port's own check,
/// that what it builds equals <c>expected.inverses</c> and <c>expected.reordered</c>, belongs
/// with the port.
/// </summary>
public class InverseVectorTest
{
    /// <summary>The label an inverse binds to the fresh fact.</summary>
    private const string PinLabel = "__f";

    public static IEnumerable<object[]> Vectors => ConformanceVector.Names("inverse");

    [Theory]
    [MemberData(nameof(Vectors))]
    public async Task TheInversesFindTheCandidates(string name)
    {
        var vector = ConformanceVector.Load("inverse", name);

        var rows = await ReadEach(vector, "inverses");

        rows.Should().BeEquivalentTo(Candidates(vector));
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public async Task TheReorderedInversesFindTheCandidates(string name)
    {
        var vector = ConformanceVector.Load("inverse", name);

        var rows = await ReadEach(vector, "reordered");

        rows.Should().BeEquivalentTo(Candidates(vector));
    }

    /// <summary>
    /// Reads every inverse of <paramref name="which"/> once for each fresh fact it can bind,
    /// and collects the rows they find. An inverse is an ordinary specification whose givens
    /// are the subscription's followed by <see cref="PinLabel"/>, so it is read from the
    /// subscription's givens and that one fact, on a store holding the graph and the whole
    /// batch. Several inverses can find one row, so the rows are a set, as the candidates are.
    /// </summary>
    private static async Task<ImmutableSortedSet<string>> ReadEach(ConformanceVector vector, string which)
    {
        var (store, references) = PlaceGraph(vector);
        var rowLabels = RowLabels(vector);
        var givens = vector.Json.GetProperty("givens");
        var rows = ImmutableSortedSet<string>.Empty;
        foreach (var arrival in Arrivals(vector))
        {
            var fresh = references[IdOf(arrival)];
            foreach (var inverse in vector.Expected.GetProperty(which).EnumerateArray())
            {
                // A vector's own projection may name one component twice, which
                // CompoundProjection cannot hold. Rows are read off the tuple by the
                // vector's rowLabels, so the projection read with it does not matter.
                var specification = ConformanceVector.ToSpecification(
                    inverse.GetProperty("specification"),
                    new SimpleProjection(rowLabels[0], typeof(object)));
                if (specification.Givens.Single(given => given.Label.Name == PinLabel).Label.Type != fresh.Type)
                {
                    // This inverse reports a fact of another type. Another one reports this.
                    continue;
                }
                var start = specification.Givens.Aggregate(
                    FactReferenceTuple.Empty,
                    (tuple, given) => tuple.Add(given.Label.Name, given.Label.Name == PinLabel
                        ? fresh
                        : references[givens.GetProperty(given.Label.Name).GetInt32()]));

                var products = await store.Read(start, specification, CancellationToken.None);

                rows = rows.Union(products.Select(product =>
                    Row(rowLabels.Select(label => product.GetFactReference(label).Hash))));
            }
        }
        return rows;
    }

    /// <summary>
    /// A store holding the vector's graph and every fact that arrives, each identified by the
    /// reference the reference implementation gives it. A vector's graph may name a predecessor
    /// content addressing cannot reach -- a fact saved later, or a cycle -- so the facts cannot
    /// be hashed, and 18 of the 30 vectors need that.
    /// </summary>
    private static (MemoryStore Store, ImmutableDictionary<int, FactReference> References) PlaceGraph(ConformanceVector vector)
    {
        var facts = vector.Json.GetProperty("graph").EnumerateArray()
            .Concat(Arrivals(vector))
            .ToImmutableList();
        var references = facts.ToImmutableDictionary(
            IdOf,
            fact => new FactReference(fact.GetProperty("type").GetString()!, "h" + IdOf(fact)));
        var store = new MemoryStore();
        store.PlaceGraphByReference(facts.Select(fact => (
            references[IdOf(fact)],
            // A fact lists one entry per predecessor, and a role may appear more than once.
            fact.GetProperty("predecessors").EnumerateArray()
                .Select(predecessor => (Predecessor)new PredecessorSingle(
                    predecessor.GetProperty("role").GetString()!,
                    references[IdOf(predecessor)]))
                .ToImmutableList())));
        return (store, references);
    }

    /// <summary>The facts that arrive: a batch, saved together, or a single fact.</summary>
    private static IEnumerable<JsonElement> Arrivals(ConformanceVector vector) =>
        vector.Json.TryGetProperty("batch", out var batch)
            ? batch.EnumerateArray()
            : ImmutableList.Create(vector.Json.GetProperty("fact"));

    private static ImmutableList<string> RowLabels(ConformanceVector vector) =>
        vector.Json.GetProperty("rowLabels").EnumerateArray()
            .Select(label => label.GetString()!)
            .ToImmutableList();

    private static ImmutableSortedSet<string> Candidates(ConformanceVector vector) =>
        vector.Expected.GetProperty("candidates").EnumerateArray()
            .Select(row => Row(row.EnumerateArray().Select(id => "h" + id.GetInt32())))
            .ToImmutableSortedSet();

    private static int IdOf(JsonElement json) => json.GetProperty("id").GetInt32();

    private static string Row(IEnumerable<string> hashes) => string.Join(",", hashes);
}
