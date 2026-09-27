using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jinaga.Pipelines;
using Jinaga.Projections;

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

    public static IEnumerable<object[]> Names(string kind) =>
        Directory.GetFiles(DirectoryOf(kind), "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new object[] { name! });

    public static ConformanceVector Load(string kind, string name)
    {
        var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(DirectoryOf(kind), name + ".json")));
        return new ConformanceVector(name, json.RootElement.Clone());
    }

    private static string DirectoryOf(string kind) =>
        Path.Combine(AppContext.BaseDirectory, "Conformance", "Vectors", kind);

    public static Specification ToSpecification(JsonElement json)
    {
        var givens = json.GetProperty("given").EnumerateArray()
            .Select(given => new SpecificationGiven(
                ToLabel(given.GetProperty("label")),
                given.GetProperty("conditions").EnumerateArray().Select(ToExistential).ToImmutableList()))
            .ToImmutableList();
        var matches = json.GetProperty("matches").EnumerateArray().Select(ToMatch).ToImmutableList();
        return new Specification(givens, matches, ToProjection(json.GetProperty("projection")));
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
            case var other:
                throw new NotSupportedException($"The vector loader does not read a projection of type '{other}'.");
        }
    }
}
