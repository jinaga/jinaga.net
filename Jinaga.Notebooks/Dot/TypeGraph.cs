using Jinaga.Repository;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Jinaga.Notebooks.Dot;

/// <summary>
/// How a fact type is marked. The compact graph folds a fact's deletion marker
/// into the fact it marks, and records the fold as a marking. The problem has
/// these three cases and no others.
/// </summary>
internal enum Marking
{
    None,
    Deletable,
    Restorable
}

/// <summary>
/// How many predecessors a role names. A role names one, or many, or one that
/// the fact may leave out. The problem has these three cases and no others.
/// </summary>
internal enum Cardinality
{
    One,
    Many,
    Optional
}

/// <summary>
/// A fact type in a graph, and how it is marked. Its name follows from the
/// class, so it is read from the class rather than carried beside it.
/// </summary>
internal sealed record FactTypeNode(Type FactClass, Marking Marking)
{
    public string Name => FactClass.FactTypeName();
}

/// <summary>
/// One role by which a fact type names a predecessor.
/// </summary>
internal sealed record PredecessorEdge(
    Type Successor,
    string Role,
    Type Predecessor,
    Cardinality Cardinality)
{
    public string SuccessorName => Successor.FactTypeName();
    public string PredecessorName => Predecessor.FactTypeName();
}

/// <summary>
/// The fact types reachable from a set of types, and the predecessor roles that
/// connect them. The type graph is walked once, in <see cref="Discover"/>. The
/// compact graph is this graph with <see cref="CollapseDeletion"/> applied, and
/// a DOT document is this graph written out by <see cref="ToDot"/>.
/// </summary>
internal sealed class TypeGraph
{
    public ImmutableList<FactTypeNode> Nodes { get; }
    public ImmutableList<PredecessorEdge> Edges { get; }

    private TypeGraph(ImmutableList<FactTypeNode> nodes, ImmutableList<PredecessorEdge> edges)
    {
        Nodes = nodes;
        Edges = edges;
    }

    /// <summary>
    /// Walks the fact types given and the predecessors they reach, breadth
    /// first. Each fact type is reported once however many roles reach it.
    /// </summary>
    public static TypeGraph Discover(params Type[] types)
    {
        // NullabilityInfoContext caches as it reads and is not thread safe, so
        // one walk gets one of its own.
        var nullability = new NullabilityInfoContext();
        var nodes = ImmutableList<FactTypeNode>.Empty;
        var edges = ImmutableList<PredecessorEdge>.Empty;
        var discovered = ImmutableHashSet<Type>.Empty;
        var toVisit = ImmutableQueue<Type>.Empty;

        foreach (var type in types.Where(IsFactType))
        {
            if (!discovered.Contains(type))
            {
                discovered = discovered.Add(type);
                toVisit = toVisit.Enqueue(type);
            }
        }

        while (!toVisit.IsEmpty)
        {
            toVisit = toVisit.Dequeue(out var factClass);
            nodes = nodes.Add(new FactTypeNode(factClass, Marking.None));
            foreach (var edge in Predecessors(factClass, nullability))
            {
                edges = edges.Add(edge);
                if (!discovered.Contains(edge.Predecessor))
                {
                    discovered = discovered.Add(edge.Predecessor);
                    toVisit = toVisit.Enqueue(edge.Predecessor);
                }
            }
        }

        return new TypeGraph(nodes, edges);
    }

    /// <summary>
    /// Folds each deletion marker into the fact it marks. A fact "X" whose only
    /// marker successor is "X.Deleted", itself with no other predecessors or
    /// successors, becomes deletable, and "X.Deleted" is left out. It becomes
    /// restorable when that deletion's only successor is "X.Restored", under the
    /// same conditions; both are left out.
    /// </summary>
    public TypeGraph CollapseDeletion()
    {
        var markers = MarkerTypes();
        var nodes = Nodes
            .Where(node => !markers.Contains(node.FactClass))
            .Select(node => node with { Marking = MarkingOf(node) })
            .ToImmutableList();
        var edges = Edges
            .Where(edge => !markers.Contains(edge.Successor) && !markers.Contains(edge.Predecessor))
            .ToImmutableList();
        return new TypeGraph(nodes, edges);
    }

    /// <summary>
    /// Writes the graph as a DOT document. Colors, labels, quoting and escaping
    /// are decided only here.
    /// </summary>
    public string ToDot()
    {
        var lines = ImmutableList.Create(
            "digraph {",
            "    rankdir=BT");
        foreach (var node in Nodes)
        {
            var name = DotWriter.Quote(node.Name);
            var fill = Fill(node.Marking);
            if (fill != null)
            {
                lines = lines.Add($"    {name} [style=filled, fillcolor={fill}]");
            }
            var edges = Edges.Where(edge => edge.Successor == node.FactClass).ToImmutableList();
            if (edges.Count > 0)
            {
                lines = lines.AddRange(edges.Select(edge =>
                    $"    {name} -> {DotWriter.Quote(edge.PredecessorName)}" +
                    $" [label=\" {edge.Role}{Punctuation(edge.Cardinality)}\"]"));
            }
            else if (fill == null)
            {
                lines = lines.Add($"    {name}");
            }
        }
        return string.Join("\n", lines.Add("}"));
    }

    private static string Fill(Marking marking)
    {
        switch (marking)
        {
            case Marking.None: return null;
            case Marking.Deletable: return "orange";
            case Marking.Restorable: return "greenyellow";
            default: throw new ArgumentException($"Unknown marking {marking}", nameof(marking));
        }
    }

    private static string Punctuation(Cardinality cardinality)
    {
        switch (cardinality)
        {
            case Cardinality.One: return "";
            case Cardinality.Many: return "*";
            case Cardinality.Optional: return "?";
            default: throw new ArgumentException($"Unknown cardinality {cardinality}", nameof(cardinality));
        }
    }

    private static IEnumerable<PredecessorEdge> Predecessors(Type factClass, NullabilityInfoContext nullability)
    {
        foreach (var property in factClass.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propertyType = property.PropertyType;
            if (propertyType.IsArray)
            {
                var elementType = propertyType.GetElementType();
                if (elementType != null && IsFactType(elementType))
                {
                    yield return new PredecessorEdge(
                        factClass, property.Name, elementType, Cardinality.Many);
                }
            }
            else if (IsFactType(propertyType))
            {
                yield return new PredecessorEdge(
                    factClass, property.Name, propertyType,
                    IsNullable(property, nullability)
                        ? Cardinality.Optional
                        : Cardinality.One);
            }
        }
    }

    private static bool IsFactType(Type type)
    {
        return type.GetCustomAttributes<FactTypeAttribute>(inherit: false).Any();
    }

    // Jinaga leaves a null single predecessor out of the fact, so a predecessor
    // whose property is annotated nullable is optional. A model compiled without
    // nullable annotations reports Unknown, which is not a claim that the
    // predecessor is optional.
    private static bool IsNullable(PropertyInfo property, NullabilityInfoContext nullability)
    {
        return nullability.Create(property).ReadState == NullabilityState.Nullable;
    }

    // A marker is named for the fact it marks: "Corporate.Office" is deleted by
    // "Corporate.Office.Deleted" and restored by "Corporate.Office.Restored".
    private static bool IsMarkerOf(Type marker, Type fact, string suffix)
    {
        return marker.FactTypeName() == fact.FactTypeName() + suffix;
    }

    private ImmutableArray<Type> SuccessorsOf(Type factClass)
    {
        return Edges
            .Where(edge => edge.Predecessor == factClass)
            .Select(edge => edge.Successor)
            .Distinct()
            .ToImmutableArray();
    }

    private bool SolePredecessor(Type factClass, Type expected)
    {
        var predecessors = Edges
            .Where(edge => edge.Successor == factClass)
            .Select(edge => edge.Predecessor)
            .ToImmutableArray();
        return predecessors.Length > 0 && predecessors.All(predecessor => predecessor == expected);
    }

    private bool IsBareDeletion(Type fact, Type deleted)
    {
        return IsMarkerOf(deleted, fact, ".Deleted")
            && SolePredecessor(deleted, fact)
            && SuccessorsOf(deleted).Length == 0;
    }

    private bool TryRestoredDeletion(Type fact, Type deleted, out Type restored)
    {
        restored = null;
        if (!IsMarkerOf(deleted, fact, ".Deleted") || !SolePredecessor(deleted, fact))
        {
            return false;
        }
        var successors = SuccessorsOf(deleted);
        if (successors.Length != 1 || !IsMarkerOf(successors[0], fact, ".Restored"))
        {
            return false;
        }
        restored = successors[0];
        return SolePredecessor(restored, deleted)
            && SuccessorsOf(restored).Length == 0;
    }

    private ImmutableHashSet<Type> MarkerTypes()
    {
        var markers = ImmutableHashSet.CreateBuilder<Type>();
        foreach (var node in Nodes)
        {
            foreach (var deleted in SuccessorsOf(node.FactClass))
            {
                if (IsBareDeletion(node.FactClass, deleted))
                {
                    markers.Add(deleted);
                }
                else if (TryRestoredDeletion(node.FactClass, deleted, out var restored))
                {
                    markers.Add(deleted);
                    markers.Add(restored);
                }
            }
        }
        return markers.ToImmutable();
    }

    // A graph that is already collapsed has no markers left to find, so a
    // marking it already carries stands rather than being recomputed away.
    private Marking MarkingOf(FactTypeNode node)
    {
        var deletable = false;
        foreach (var deleted in SuccessorsOf(node.FactClass))
        {
            if (TryRestoredDeletion(node.FactClass, deleted, out _))
            {
                return Marking.Restorable;
            }
            if (IsBareDeletion(node.FactClass, deleted))
            {
                deletable = true;
            }
        }
        return deletable ? Marking.Deletable : node.Marking;
    }
}
