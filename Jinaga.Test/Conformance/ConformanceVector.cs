using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Storage;

namespace Jinaga.Test.Conformance;

/// <summary>
/// A conformance vector from jinaga-spec, read from the pinned copy under
/// <c>Conformance/Vectors</c>. See <c>Conformance/README.md</c>.
///
/// A vector's specification is JSON in the shape jinaga.js serializes. Each match there holds
/// one list of conditions; <see cref="Match"/> here keeps path and existential conditions apart,
/// so the loader partitions them and keeps their order within each kind.
/// </summary>
public class ConformanceVector
{
    private ConformanceVector(string name, JsonElement json)
    {
        Name = name;
        Json = json;
    }

    public string Name { get; }
    public JsonElement Json { get; }

    public Specification Specification => ToSpecification(Json.GetProperty("specification"));
    public string Text => Json.GetProperty("text").GetString()!;
    public JsonElement Expected => Json.GetProperty("expected");

    /// <summary>The projection's labels: a row lists their facts, in this order.</summary>
    public ImmutableList<string> RowLabels => Json.GetProperty("rowLabels").EnumerateArray()
        .Select(label => label.GetString()!)
        .ToImmutableList();

    /// <summary>The facts the vector's graph holds, before anything arrives.</summary>
    public IEnumerable<JsonElement> Graph => Json.GetProperty("graph").EnumerateArray();

    /// <summary>The fact id the subscription binds to each given label.</summary>
    public ImmutableDictionary<string, int> Givens => Json.GetProperty("givens").EnumerateObject()
        .ToImmutableDictionary(given => given.Name, given => given.Value.GetInt32());

    public static IEnumerable<object[]> Names(string kind) =>
        Directory.GetFiles(DirectoryOf(kind), "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new object[] { name! });

    public static ConformanceVector Load(string kind, string name)
    {
        // The clone owns its own memory, so the document can return its pooled buffers.
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(DirectoryOf(kind), name + ".json")));
        var root = json.RootElement.Clone();
        CheckFields(kind, name, root);
        return new ConformanceVector(name, root);
    }

    /// <summary>
    /// The fields each kind of vector carries, and the fields of its <c>expected</c>.
    /// README.md names the test that reads each one.
    /// </summary>
    private static readonly ImmutableDictionary<string, (string[] Vector, string[] Expected)> KnownFields =
        ImmutableDictionary<string, (string[], string[])>.Empty
            .Add("well-formed", (
                new[] { "name", "description", "source", "specification", "text", "violates", "expected" },
                new[] { "wellFormed" }))
            .Add("split", (
                new[] { "name", "description", "source", "specification", "text", "expected" },
                new[] { "head", "headText", "tail", "tailText", "tailReadsGiven" }))
            .Add("inverse", (
                new[] { "name", "description", "source", "specification", "text", "rowLabels", "givens", "graph", "fact", "batch", "expected" },
                new[] { "gained", "lost", "inverses", "reordered", "candidates", "reduced", "exact" }))
            .Add("feeds", (
                new[] { "name", "description", "source", "specification", "text", "rowLabels", "givens", "graph", "expected" },
                new[] { "feeds", "delivered", "results", "nestedResults" }));

    /// <summary>
    /// Refuses a vector that carries a field this loader does not know. A re-pin that adds one
    /// states something no test here reads, so it fails loudly rather than passing unnoticed.
    /// </summary>
    private static void CheckFields(string kind, string name, JsonElement json)
    {
        if (!KnownFields.TryGetValue(kind, out var known))
        {
            throw new NotSupportedException($"The vector loader does not read vectors of kind '{kind}'.");
        }
        Check(json, known.Vector, $"The {kind} vector '{name}'");
        Check(json.GetProperty("expected"), known.Expected, $"The expected of the {kind} vector '{name}'");

        static void Check(JsonElement element, string[] known, string description)
        {
            var unknown = element.EnumerateObject()
                .Select(field => field.Name)
                .Where(field => !known.Contains(field))
                .ToImmutableList();
            if (!unknown.IsEmpty)
            {
                throw new NotSupportedException(
                    $"{description} carries the field(s) {string.Join(", ", unknown.Select(field => $"'{field}'"))}, " +
                    "which the vector loader does not read. Extend it, and say in Conformance/README.md which test reads the field.");
            }
        }
    }

    /// <summary>
    /// A store holding <paramref name="facts"/>, each identified by the reference the reference
    /// implementation gives it. A vector's graph may name a predecessor content addressing
    /// cannot reach -- a fact saved later, or a cycle -- so the facts cannot be hashed, and
    /// many of the vectors need that.
    /// </summary>
    public static (MemoryStore Store, ImmutableDictionary<int, FactReference> References) PlaceGraph(
        IEnumerable<JsonElement> facts)
    {
        var all = facts.ToImmutableList();
        var references = all.ToImmutableDictionary(
            IdOf,
            fact => new FactReference(fact.GetProperty("type").GetString()!, "h" + IdOf(fact)));
        var store = new MemoryStore();
        store.PlaceGraphByReference(all.Select(fact => (
            references[IdOf(fact)],
            // A fact lists one entry per predecessor, and a role may appear more than once.
            fact.GetProperty("predecessors").EnumerateArray()
                .Select(predecessor => (Predecessor)new PredecessorSingle(
                    predecessor.GetProperty("role").GetString()!,
                    references[IdOf(predecessor)]))
                .ToImmutableList())));
        return (store, references);
    }

    public static int IdOf(JsonElement json) => json.GetProperty("id").GetInt32();

    private static string DirectoryOf(string kind) =>
        Path.Combine(AppContext.BaseDirectory, "Conformance", "Vectors", kind);

    public static Specification ToSpecification(JsonElement json) =>
        ToSpecification(json, ToProjection(json.GetProperty("projection")));

    /// <summary>
    /// Reads a specification, projecting <paramref name="projection"/> instead of the one the
    /// JSON carries. An inverse vector's projection may name one component twice, which
    /// <see cref="CompoundProjection"/> cannot hold; its rows are compared by the vector's
    /// <c>rowLabels</c> read off the tuple, so the projection it is read with does not matter.
    /// </summary>
    public static Specification ToSpecification(JsonElement json, Projection projection)
    {
        var givens = json.GetProperty("given").EnumerateArray()
            .Select(given => new SpecificationGiven(
                ToLabel(given.GetProperty("label")),
                given.GetProperty("conditions").EnumerateArray().Select(ToExistential).ToImmutableList()))
            .ToImmutableList();
        var matches = json.GetProperty("matches").EnumerateArray().Select(ToMatch).ToImmutableList();
        return new Specification(givens, matches, projection);
    }

    private static Match ToMatch(JsonElement json)
    {
        var conditions = json.GetProperty("conditions").EnumerateArray().ToList();
        return new Match(
            ToLabel(json.GetProperty("unknown")),
            conditions
                .Where(condition => condition.GetProperty("type").GetString() == "path")
                .Select(ToPath)
                .ToImmutableList(),
            conditions
                .Where(condition => condition.GetProperty("type").GetString() == "existential")
                .Select(ToExistential)
                .ToImmutableList());
    }

    private static PathCondition ToPath(JsonElement json) =>
        new(
            ToRoles(json.GetProperty("rolesLeft")),
            json.GetProperty("labelRight").GetString()!,
            ToRoles(json.GetProperty("rolesRight")));

    private static ExistentialCondition ToExistential(JsonElement json) =>
        new(
            json.GetProperty("exists").GetBoolean(),
            json.GetProperty("matches").EnumerateArray().Select(ToMatch).ToImmutableList());

    private static ImmutableList<Role> ToRoles(JsonElement json) =>
        json.EnumerateArray()
            .Select(role => new Role(role.GetProperty("name").GetString()!, role.GetProperty("predecessorType").GetString()!))
            .ToImmutableList();

    private static Label ToLabel(JsonElement json) =>
        new(json.GetProperty("name").GetString()!, json.GetProperty("type").GetString()!);

    private static Projection ToProjection(JsonElement json)
    {
        switch (json.GetProperty("type").GetString())
        {
            case "fact":
                return new SimpleProjection(json.GetProperty("label").GetString()!, typeof(object));
            case "composite":
                return new CompoundProjection(
                    json.GetProperty("components").EnumerateArray().ToImmutableDictionary(
                        component => component.GetProperty("name").GetString()!,
                        component => ToProjection(component)),
                    typeof(object));
            case "specification":
                // A child specification, as a component of a composite projection. Its matches
                // continue from its parent's, which is what CollectionProjection holds.
                return new CollectionProjection(
                    json.GetProperty("matches").EnumerateArray().Select(ToMatch).ToImmutableList(),
                    ToProjection(json.GetProperty("projection")),
                    typeof(object));
            case var other:
                throw new NotSupportedException($"The vector loader does not read a projection of type '{other}'.");
        }
    }
}
