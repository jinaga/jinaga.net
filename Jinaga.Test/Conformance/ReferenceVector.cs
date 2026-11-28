using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jinaga.Projections;

namespace Jinaga.Test.Conformance;

/// <summary>
/// A reference vector from jinaga.js, read from the pinned copy under
/// <c>Conformance/Reference</c>. See <c>Conformance/README.md</c>.
///
/// <para>
/// These are not the jinaga-spec vectors <see cref="ConformanceVector"/> reads. jinaga-spec
/// does not define skeletons, so jinaga.js's implementation is the reference for them, and
/// its output reaches this repository as these vectors rather than as strings copied into a
/// test. A vector carries a specification, the feeds jinaga.js's own <c>buildFeeds</c>
/// produces for it, and the skeleton of each of those feeds.
/// </para>
/// </summary>
public class ReferenceVector
{
    private ReferenceVector(string name, JsonElement json)
    {
        Name = name;
        Json = json;
    }

    public string Name { get; }
    public JsonElement Json { get; }

    public Specification Specification => ConformanceVector.ToSpecification(Json.GetProperty("specification"));
    public string Text => Json.GetProperty("text").GetString()!;

    /// <summary>The feeds jinaga.js builds, in the order it returns them.</summary>
    public ImmutableList<ReferenceFeed> Feeds => Json.GetProperty("feeds").EnumerateArray()
        .Select(feed => new ReferenceFeed(feed))
        .ToImmutableList();

    public static IEnumerable<object[]> Names(string kind) =>
        Directory.GetFiles(DirectoryOf(kind), "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new object[] { name! });

    public static ReferenceVector Load(string kind, string name)
    {
        // The clone owns its own memory, so the document can return its pooled buffers.
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(DirectoryOf(kind), name + ".json")));
        var root = json.RootElement.Clone();
        CheckFields(kind, name, root);
        return new ReferenceVector(name, root);
    }

    private static readonly string[] VectorFields = { "name", "text", "specification", "feeds" };
    private static readonly string[] FeedFields = { "text", "specification", "skeleton" };
    private static readonly string[] SkeletonFields =
        { "facts", "inputs", "edges", "notExistsConditions", "outputs" };
    private static readonly string[] FactFields = { "factType", "factIndex" };
    private static readonly string[] InputFields = { "factIndex", "inputIndex" };
    private static readonly string[] EdgeFields =
        { "edgeIndex", "predecessorFactIndex", "successorFactIndex", "roleName" };
    private static readonly string[] ConditionFields = { "edges", "notExistsConditions" };
    private static readonly string[] OutputFields = { "factIndex" };

    /// <summary>
    /// Refuses a vector carrying a field this loader does not read, in the vector itself, in
    /// one of its feeds, or anywhere in a feed's skeleton. A re-pin that adds one states
    /// something no test here reads, so it fails loudly rather than passing unnoticed.
    ///
    /// <para>
    /// Unlike <see cref="ConformanceVector"/>, this descends all the way into the skeleton,
    /// through every fact, input, edge and output and through each nested not-exists
    /// condition. That loader can stop at a specification's own interior because a
    /// specification is also stated as <c>text</c>, so a shape it silently dropped shows up
    /// as the text not matching. A skeleton has no second statement of itself: it <em>is</em>
    /// the data under test, and <see cref="ReferenceFeed.ToSkeleton"/> reads it field by
    /// field, so a field added inside an edge would be dropped with nothing left to detect
    /// it. It still stops at the specifications, which
    /// <see cref="ConformanceVector.ToSpecification"/> reads.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Internal rather than private so that <see cref="ReferenceVectorLoaderTest"/> can put a
    /// synthetic vector through it. The pinned vectors are all well formed by construction, so
    /// nothing else here would ever exercise a refusal.
    /// </remarks>
    internal static void CheckFields(string kind, string name, JsonElement json)
    {
        if (kind != "feeds")
        {
            throw new NotSupportedException($"The reference vector loader does not read vectors of kind '{kind}'.");
        }
        var where = $"the reference vector '{name}'";
        Check(json, VectorFields, $"The reference vector '{name}'");
        foreach (var feed in json.GetProperty("feeds").EnumerateArray())
        {
            Check(feed, FeedFields, $"A feed of {where}");
            var skeleton = feed.GetProperty("skeleton");
            Check(skeleton, SkeletonFields, $"A feed skeleton of {where}");
            CheckEach(skeleton, "facts", FactFields, $"A skeleton fact of {where}");
            CheckEach(skeleton, "inputs", InputFields, $"A skeleton input of {where}");
            CheckEach(skeleton, "edges", EdgeFields, $"A skeleton edge of {where}");
            CheckEach(skeleton, "outputs", OutputFields, $"A skeleton output of {where}");
            CheckConditions(skeleton, where);
        }

        static void CheckConditions(JsonElement holder, string where)
        {
            foreach (var condition in holder.GetProperty("notExistsConditions").EnumerateArray())
            {
                Check(condition, ConditionFields, $"A skeleton not-exists condition of {where}");
                CheckEach(condition, "edges", EdgeFields,
                    $"An edge of a skeleton not-exists condition of {where}");
                CheckConditions(condition, where);
            }
        }

        static void CheckEach(JsonElement holder, string field, string[] known, string description)
        {
            foreach (var element in holder.GetProperty(field).EnumerateArray())
            {
                Check(element, known, description);
            }
        }

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
                    "which the reference vector loader does not read. Extend it, and say in " +
                    "Conformance/README.md which test reads the field.");
            }
        }
    }

    private static string DirectoryOf(string kind) =>
        Path.Combine(AppContext.BaseDirectory, "Conformance", "Reference", kind);
}

/// <summary>One of the feeds a reference vector states, and the skeleton jinaga.js gives it.</summary>
public class ReferenceFeed
{
    private readonly JsonElement json;

    public ReferenceFeed(JsonElement json)
    {
        this.json = json;
    }

    public string Text => json.GetProperty("text").GetString()!;
    public Specification Specification => ConformanceVector.ToSpecification(json.GetProperty("specification"));

    /// <summary>The skeleton as jinaga.js emitted it, read into this library's own shape.</summary>
    internal Skeleton Skeleton => ToSkeleton(json.GetProperty("skeleton"));

    internal static Skeleton ToSkeleton(JsonElement json) =>
        new Skeleton(
            json.GetProperty("facts").EnumerateArray()
                .Select(fact => new FactDescription(
                    fact.GetProperty("factType").GetString()!,
                    fact.GetProperty("factIndex").GetInt32()))
                .ToImmutableList(),
            json.GetProperty("inputs").EnumerateArray()
                .Select(input => new InputDescription(
                    input.GetProperty("factIndex").GetInt32(),
                    input.GetProperty("inputIndex").GetInt32()))
                .ToImmutableList(),
            json.GetProperty("edges").EnumerateArray().Select(ToEdge).ToImmutableList(),
            json.GetProperty("notExistsConditions").EnumerateArray().Select(ToCondition).ToImmutableList(),
            json.GetProperty("outputs").EnumerateArray()
                .Select(output => new OutputDescription(output.GetProperty("factIndex").GetInt32()))
                .ToImmutableList());

    private static EdgeDescription ToEdge(JsonElement json) =>
        new(
            json.GetProperty("edgeIndex").GetInt32(),
            json.GetProperty("predecessorFactIndex").GetInt32(),
            json.GetProperty("successorFactIndex").GetInt32(),
            json.GetProperty("roleName").GetString()!);

    private static NotExistsConditionDescription ToCondition(JsonElement json) =>
        new(
            json.GetProperty("edges").EnumerateArray().Select(ToEdge).ToImmutableList(),
            json.GetProperty("notExistsConditions").EnumerateArray().Select(ToCondition).ToImmutableList());
}
