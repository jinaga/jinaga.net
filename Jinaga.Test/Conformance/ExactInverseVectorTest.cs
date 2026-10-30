using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Storage;

namespace Jinaga.Test.Conformance;

/// <summary>
/// The exact inverses of jinaga-spec, read once each with no re-check, against the changes
/// its <c>changes</c> computes (<c>JinagaSpec/Exact.lean</c>). See
/// <c>Conformance/README.md</c> for where <c>Fixtures/exact-inverse-vectors.json</c> came
/// from.
///
/// <see cref="InverseVectorTest"/> asks whether the inverses find every candidate, which
/// leaves a caller to decide each one against the store.
/// <see cref="InverseCandidates.ExactInverses"/> answers the stronger question: a row an add
/// inverse finds is in the new result, and a row a remove inverse finds is not.
///
/// Every case keys its rows by <c>keyLabels</c>, the specification's top-level unknowns, so
/// a row determines its solution. That is <c>projectsUnknowns</c>, the hypothesis the remove
/// direction needs, and each case is read with a projection of those labels so that the
/// reordering sees the same projection the vector was generated under.
/// </summary>
public class ExactInverseVectorTest
{
    /// <summary>The label an inverse binds to the fresh fact.</summary>
    private const string PinLabel = "__f";

    public static IEnumerable<object[]> Cases => Fixture.Value
        .EnumerateArray()
        .Select(json => new object[] { json.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task TheExactInversesFindTheChanges(string name)
    {
        var json = Case(name);
        var specification = Subject(json);
        var (store, references) = PlaceGraph(json);
        var keyLabels = KeyLabels(json);
        var givens = json.GetProperty("givens");

        var adds = ImmutableSortedSet<string>.Empty;
        var removes = ImmutableSortedSet<string>.Empty;
        foreach (var arrival in Arrivals(json))
        {
            var fresh = references[IdOf(arrival)];
            if (givens.EnumerateObject().Any(given => given.Value.GetInt32() == IdOf(arrival)))
            {
                // One of the subscription's givens arrives. Nothing was read before it, so
                // every row of a read of the whole specification is added and none is
                // removed: factChanges' first branch in JinagaSpec/Exact.lean.
                var start = specification.Givens.Aggregate(
                    FactReferenceTuple.Empty,
                    (tuple, given) => tuple.Add(
                        given.Label.Name,
                        references[givens.GetProperty(given.Label.Name).GetInt32()]));
                adds = adds.Union(await Read(store, specification, start, keyLabels));
                continue;
            }

            var exact = InverseCandidates.ExactInverses(specification);
            adds = adds.Union(await ReadEach(store, exact.Adds, references, fresh, givens, keyLabels));
            removes = removes.Union(await ReadEach(store, exact.Removes, references, fresh, givens, keyLabels));
        }

        var expected = json.GetProperty("expected");
        adds.Should().BeEquivalentTo(Rows(expected, "adds"));
        removes.Should().BeEquivalentTo(Rows(expected, "removes"));

        // A check on this harness rather than on the port: what the vector says changed has
        // to sit inside what the inverses report, and nothing can be both.
        Rows(expected, "gained").Should().BeSubsetOf(adds);
        Rows(expected, "lost").Should().BeSubsetOf(removes);
        adds.Intersect(removes).Should().BeEmpty();
    }

    /// <summary>
    /// An inverse that asks the same question twice reads differently from one store to the
    /// next, so <see cref="InverseCandidates"/> drops a condition it is about to repeat.
    /// <c>Match.ToDescriptiveString</c> collapses duplicates with <c>Union</c>, so this walks
    /// each match's two condition lists instead of comparing printed matches.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void NoInverseCarriesADuplicatedCondition(string name)
    {
        var exact = InverseCandidates.ExactInverses(Subject(Case(name)));

        foreach (var inverse in exact.Adds.Concat(exact.Removes))
        {
            AssertConditionsAreDistinct(inverse.Matches);
        }
    }

    private static void AssertConditionsAreDistinct(ImmutableList<Match> matches)
    {
        foreach (var match in matches)
        {
            match.PathConditions
                .Select(condition => condition.ToDescriptiveString(match.Unknown.Name, 0))
                .Should().OnlyHaveUniqueItems();
            match.ExistentialConditions
                .Select(condition => condition.ToDescriptiveString(match.Unknown.Name, 0))
                .Should().OnlyHaveUniqueItems();
            foreach (var condition in match.ExistentialConditions)
            {
                AssertConditionsAreDistinct(condition.Matches);
            }
        }
    }

    /// <summary>
    /// Reads each inverse that can bind <paramref name="fresh"/>, from the subscription's
    /// givens and that fact, and collects the rows. Several inverses can find one row, so the
    /// rows are a set.
    /// </summary>
    private static async Task<ImmutableSortedSet<string>> ReadEach(
        MemoryStore store,
        ImmutableList<Specification> inverses,
        ImmutableDictionary<int, FactReference> references,
        FactReference fresh,
        JsonElement givens,
        ImmutableList<string> keyLabels)
    {
        var rows = ImmutableSortedSet<string>.Empty;
        foreach (var inverse in inverses)
        {
            if (inverse.Givens.Single(given => given.Label.Name == PinLabel).Label.Type != fresh.Type)
            {
                // This inverse reports a fact of another type. Another one reports this.
                continue;
            }
            var start = inverse.Givens.Aggregate(
                FactReferenceTuple.Empty,
                (tuple, given) => tuple.Add(given.Label.Name, given.Label.Name == PinLabel
                    ? fresh
                    : references[givens.GetProperty(given.Label.Name).GetInt32()]));
            rows = rows.Union(await Read(store, inverse, start, keyLabels));
        }
        return rows;
    }

    /// <summary>The rows a read of <paramref name="specification"/> finds, by the case's key labels.</summary>
    private static async Task<IEnumerable<string>> Read(
        MemoryStore store,
        Specification specification,
        FactReferenceTuple start,
        ImmutableList<string> keyLabels)
    {
        var products = await store.Read(start, specification, CancellationToken.None);
        return products
            .Select(product => Row(keyLabels.Select(label => product.GetFactReference(label).Hash)))
            .ToImmutableList();
    }

    /// <summary>
    /// The case's specification, projecting its key labels. The vector's changes were
    /// generated with that projection in place of the one the specification carries, because
    /// a row there is what the projection names, and
    /// <see cref="InverseCandidates.ReorderInverse"/> reads the projection to decide whether
    /// an inverse may be reordered.
    /// </summary>
    private static Specification Subject(JsonElement json) =>
        ConformanceVector.ToSpecification(
            json.GetProperty("specification"),
            new CompoundProjection(
                KeyLabels(json).ToImmutableDictionary(
                    label => label,
                    label => (Projection)new SimpleProjection(label, typeof(object))),
                typeof(object)));

    /// <summary>
    /// A store holding the case's graph and every fact that arrives, each identified by the
    /// reference the reference implementation gives it. A graph may name a predecessor that
    /// content addressing cannot reach -- a fact saved later, or a cycle -- so the facts
    /// cannot be hashed.
    /// </summary>
    private static (MemoryStore Store, ImmutableDictionary<int, FactReference> References) PlaceGraph(JsonElement json)
    {
        var facts = json.GetProperty("graph").EnumerateArray()
            .Concat(Arrivals(json))
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
    private static IEnumerable<JsonElement> Arrivals(JsonElement json) =>
        json.TryGetProperty("batch", out var batch)
            ? batch.EnumerateArray()
            : ImmutableList.Create(json.GetProperty("fact"));

    private static ImmutableList<string> KeyLabels(JsonElement json) =>
        json.GetProperty("keyLabels").EnumerateArray()
            .Select(label => label.GetString()!)
            .ToImmutableList();

    private static ImmutableSortedSet<string> Rows(JsonElement expected, string which) =>
        expected.GetProperty(which).EnumerateArray()
            .Select(row => Row(row.EnumerateArray().Select(id => "h" + id.GetInt32())))
            .ToImmutableSortedSet();

    private static int IdOf(JsonElement json) => json.GetProperty("id").GetInt32();

    private static string Row(IEnumerable<string> hashes) => string.Join(",", hashes);

    private static JsonElement Case(string name) => Fixture.Value
        .EnumerateArray()
        .Single(json => json.GetProperty("name").GetString() == name);

    private static readonly Lazy<JsonElement> Fixture = new(() =>
    {
        // The clone owns its own memory, so the document can return its pooled buffers.
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Conformance", "Fixtures", "exact-inverse-vectors.json")));
        return json.RootElement.Clone();
    });
}
