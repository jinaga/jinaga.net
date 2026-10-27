using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Storage;

namespace Jinaga.Test.Conformance;

/// <summary>
/// The inverse vectors of jinaga-spec: the inverses its proven <c>candidates</c> algorithm
/// builds for a specification, and the reordering of each, read from the subscription's givens
/// and each fresh fact, find exactly the rows the spec calls the candidates.
///
/// These vectors gate the port two ways. They state what the rows of each list come to,
/// independently of what builds it, and they state the lists themselves, which
/// <see cref="InverseCandidates"/> must build exactly.
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

    [Theory]
    [MemberData(nameof(Vectors))]
    public void ThePortBuildsTheInverses(string name)
    {
        var vector = ConformanceVector.Load("inverse", name);

        var inverses = InverseCandidates.BuildInverses(Load(vector, vector.Json.GetProperty("specification")));

        Describe(inverses).Should().Equal(Expected(vector, "inverses"));
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void ThePortReordersTheInverses(string name)
    {
        var vector = ConformanceVector.Load("inverse", name);

        var reordered = InverseCandidates
            .BuildInverses(Load(vector, vector.Json.GetProperty("specification")))
            .Select(InverseCandidates.ReorderInverse);

        Describe(reordered).Should().Equal(Expected(vector, "reordered"));
    }

    /// <summary>
    /// The specifications of <paramref name="which"/>, as <c>ToDescriptiveString</c> prints
    /// them. Each is loaded through the same loader as the input, so both sides of the
    /// comparison partition each match's conditions into path and existential conditions the
    /// same way.
    /// </summary>
    private static ImmutableList<string> Expected(ConformanceVector vector, string which) =>
        vector.Expected.GetProperty(which).EnumerateArray()
            .Select(inverse => Load(vector, inverse.GetProperty("specification")).ToDescriptiveString())
            .ToImmutableList();

    private static ImmutableList<string> Describe(IEnumerable<Specification> specifications) =>
        specifications.Select(specification => specification.ToDescriptiveString()).ToImmutableList();

    /// <summary>
    /// Loads a specification with the projection its own JSON carries, because
    /// <see cref="InverseCandidates.ReorderInverse"/> reads it: whether every label the
    /// projection names is in scope is one of the conditions it checks, and a substitute
    /// projection would answer that question about itself. Where the projection names one
    /// component twice, which <see cref="CompoundProjection"/> cannot hold, it reads the
    /// vector's first row label instead. Every vector that needs that projects that one label
    /// in each of its components, so the labels the projection names are the same either way.
    /// </summary>
    private static Specification Load(ConformanceVector vector, JsonElement json) =>
        NamesAComponentTwice(json.GetProperty("projection"))
            ? ConformanceVector.ToSpecification(
                json,
                new SimpleProjection(RowLabels(vector)[0], typeof(object)))
            : ConformanceVector.ToSpecification(json);

    private static bool NamesAComponentTwice(JsonElement projection)
    {
        if (projection.GetProperty("type").GetString() != "composite")
        {
            return false;
        }
        var names = projection.GetProperty("components").EnumerateArray()
            .Select(component => component.GetProperty("name").GetString()!)
            .ToImmutableList();
        return names.Distinct().Count() != names.Count;
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
