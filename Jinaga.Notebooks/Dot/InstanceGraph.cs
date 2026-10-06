using Jinaga.Facts;
using Jinaga.Repository;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Web;

namespace Jinaga.Notebooks.Dot;

/// <summary>
/// A fact in a graph: how it is identified, what it shows, and whether it was
/// one of the facts asked for.
/// </summary>
internal sealed record FactNode(
    string Hash,
    string Type,
    ImmutableList<Field> Fields,
    bool Requested);

/// <summary>
/// One role by which a fact names a predecessor.
/// </summary>
internal sealed record FactEdge(string Successor, string Role, string Predecessor);

/// <summary>
/// How much of a fact graph to show: how deep <see cref="InstanceGraph.Discover(JinagaClient, InstanceGraphOptions, object[])"/>
/// looks into a projection object for facts, and how much of a string field
/// <see cref="InstanceGraph.ToDot"/> writes before it cuts it short.
/// </summary>
public sealed record InstanceGraphOptions
{
    /// <summary>
    /// What the overloads that take no options pass on.
    /// </summary>
    public static readonly InstanceGraphOptions Default = new();

    /// <summary>
    /// How deep into a projection object to look for facts. A fact at any depth
    /// from 0 through this value is found, so a projection that nests facts
    /// deeper needs a greater value to show them.
    /// </summary>
    public int SearchDepth { get; init; } = 5;

    /// <summary>
    /// How many characters of a string field to show. A longer value is cut to
    /// this length and followed by an ellipsis.
    /// </summary>
    public int FieldLength { get; init; } = 20;
}

/// <summary>
/// The facts found in a set of projections, together with their predecessors and
/// the roles that connect them. The facts are collected once, in
/// <see cref="Discover"/>, and a DOT document is this graph written out by
/// <see cref="ToDot"/>.
/// </summary>
internal sealed class InstanceGraph
{
    // Only the field length outlives discovery. The search depth is spent
    // finding the facts, so the graph that comes out of it does not vary by it.
    private readonly int fieldLength;

    public ImmutableList<FactNode> Nodes { get; }
    public ImmutableList<FactEdge> Edges { get; }

    private InstanceGraph(ImmutableList<FactNode> nodes, ImmutableList<FactEdge> edges, int fieldLength)
    {
        Nodes = nodes;
        Edges = edges;
        this.fieldLength = fieldLength;
    }

    /// <summary>
    /// Finds the facts within the projections given, and the predecessors they
    /// reach, showing as much of each fact as the default options allow.
    /// </summary>
    public static InstanceGraph Discover(JinagaClient jinagaClient, params object[] projections)
    {
        return Discover(jinagaClient, InstanceGraphOptions.Default, projections);
    }

    /// <summary>
    /// Finds the facts within the projections given, and the predecessors they
    /// reach. A fact that was asked for is marked as requested. The options say
    /// how deep to look and how much of a string field to show.
    /// </summary>
    public static InstanceGraph Discover(JinagaClient jinagaClient, InstanceGraphOptions options, params object[] projections)
    {
        var graph = FactGraph.Empty;
        var requested = ImmutableHashSet<FactReference>.Empty;
        foreach (var fact in projections.SelectMany(projection => GetFacts(projection, options.SearchDepth)))
        {
            var factGraph = jinagaClient.Graph(fact);
            graph = graph.AddGraph(factGraph);
            requested = requested.Add(factGraph.Last);
        }

        var nodes = ImmutableList<FactNode>.Empty;
        var edges = ImmutableList<FactEdge>.Empty;
        foreach (var reference in graph.FactReferences)
        {
            var fact = graph.GetFact(reference);
            nodes = nodes.Add(new FactNode(
                reference.Hash,
                reference.Type,
                fact.Fields,
                requested.Contains(reference)));
            foreach (var predecessor in fact.Predecessors)
            {
                foreach (var predecessorReference in References(predecessor))
                {
                    edges = edges.Add(new FactEdge(
                        reference.Hash, predecessor.Role, predecessorReference.Hash));
                }
            }
        }
        return new InstanceGraph(nodes, edges, options.FieldLength);
    }

    /// <summary>
    /// Writes the graph as a DOT document. Labels, encoding, the display of a
    /// null field, number formatting, quoting and escaping are decided only
    /// here.
    /// </summary>
    public string ToDot()
    {
        var lines = ImmutableList.Create(
            "digraph {",
            "    rankdir=BT",
            "    node [shape=none]");
        foreach (var node in Nodes)
        {
            lines = lines.Add($"    {DotWriter.Quote(node.Hash)} [label=<{Label(node)}>]");
            lines = lines.AddRange(Edges
                .Where(edge => edge.Successor == node.Hash)
                .Select(edge =>
                    $"    {DotWriter.Quote(edge.Successor)} -> {DotWriter.Quote(edge.Predecessor)}" +
                    $" [label=\" {edge.Role}\"]"));
        }
        return string.Join("\n", lines.Add("}"));
    }

    private string Label(FactNode node)
    {
        string typeRow = @$"<TR><TD COLSPAN=""2"">{Encode(node.Type)}</TD></TR>";
        var fieldRows = string.Join("", node.Fields
            .Select(field => $"<TR><TD>{Encode(field.Name)}</TD><TD>{Display(field.Value)}</TD></TR>"));
        int border = node.Requested ? 1 : 0;
        return @$"<TABLE BORDER=""{border}"" CELLBORDER=""1"" CELLSPACING=""0"">{typeRow}{fieldRows}</TABLE>";
    }

    // One encoding for everything in an HTML label, so a name with a space, an
    // ampersand or a letter outside ASCII shows as it was written.
    private static string Encode(string text)
    {
        return HttpUtility.HtmlEncode(text);
    }

    // Every null shows as "null", whatever the C# type that produced it, so that
    // a null is never shown the way an empty string is shown.
    private string Display(FieldValue value)
    {
        switch (value)
        {
            case FieldValueString str:
                return str.StringValue == null ? "null" : Encode(Limit(str.StringValue));
            case FieldValueNumber number:
                return number.DoubleValue.ToString(CultureInfo.InvariantCulture);
            case FieldValueBoolean b:
                return b.BoolValue ? "true" : "false";
            case FieldValueNull _:
                return "null";
            default:
                throw new NotImplementedException($"Cannot display a {value.GetType().Name}");
        }
    }

    private string Limit(string stringValue)
    {
        return stringValue.Length > fieldLength
            ? $"{stringValue.Substring(0, fieldLength)}..."
            : stringValue;
    }

    private static IEnumerable<FactReference> References(Predecessor predecessor)
    {
        switch (predecessor)
        {
            case PredecessorSingle single:
                return new[] { single.Reference };
            case PredecessorMultiple multiple:
                return multiple.References;
            default:
                return Enumerable.Empty<FactReference>();
        }
    }

    private static IEnumerable<object> GetFacts(object projection, int depth)
    {
        if (depth < 0)
        {
            yield break;
        }
        if (projection == null)
        {
            yield break;
        }
        var type = projection.GetType();
        if (type.IsFactType())
        {
            yield return projection;
        }
        else if (type.IsAssignableTo(typeof(IEnumerable)) && type != typeof(string))
        {
            var collection = (IEnumerable)projection;
            foreach (var item in collection)
            {
                foreach (var child in GetFacts(item, depth - 1))
                {
                    yield return child;
                }
            }
        }
        else
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.GetIndexParameters().Any())
                {
                    foreach (var child in GetFacts(property.GetValue(projection), depth - 1))
                    {
                        yield return child;
                    }
                }
            }
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var child in GetFacts(field.GetValue(projection), depth - 1))
                {
                    yield return child;
                }
            }
        }
    }
}
