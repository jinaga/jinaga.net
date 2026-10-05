using Jinaga.Repository;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Jinaga.Notebooks.Dot;

public static class Renderer
{
    public enum Cardinality
    {
        One,
        Many,
        Optional
    }
    public static string RenderTypes(params Type[] types)
    {
        string[] prefix = new[]
        {
            "digraph {",
            "    rankdir=BT",
        };
        string[] suffix = new[]
        {
            "}"
        };
        var toVisit = types
            .Where(t => t.GetCustomAttributes(typeof(FactTypeAttribute), false).Any())
            .Distinct()
            .ToImmutableList();
        var visited = ImmutableList<Type>.Empty;
        var lines = ImmutableList<string>.Empty;
        while (toVisit.Any())
        {
            (toVisit, visited, lines) = VisitFactType(toVisit, visited, lines);
        }
        string graph = string.Join("\n", prefix.Concat(lines).Concat(suffix));
        return graph;
    }

    public static string RenderTypesCompact(params Type[] types)
    {
        string[] prefix = new[]
        {
            "digraph {",
            "    rankdir=BT",
        };
        string[] suffix = new[]
        {
            "}"
        };
        var toVisit = types
            .Where(t => t.GetCustomAttributes(typeof(FactTypeAttribute), false).Any())
            .Distinct()
            .ToImmutableList();
        var visited = ImmutableList<Type>.Empty;
        var predecessorsByType = ImmutableDictionary<Type, ImmutableArray<CompactPredecessor>>.Empty;
        while (toVisit.Any())
        {
            var factClass = toVisit.First();
            var predecessors = FactPredecessors(factClass);
            var newToVisit = predecessors
                .Select(predecessor => predecessor.Type)
                .Where(type => !toVisit.Contains(type) && !visited.Contains(type));
            toVisit = toVisit.Skip(1).Concat(newToVisit).ToImmutableList();
            visited = visited.Add(factClass);
            if (!predecessorsByType.ContainsKey(factClass))
                predecessorsByType = predecessorsByType.Add(factClass, predecessors);
        }

        var hidden = HiddenDeleteRestore(predecessorsByType);
        var lines = ImmutableList<string>.Empty;
        foreach (var factClass in visited)
        {
            if (hidden.Contains(factClass))
                continue;

            var left = factClass.FactTypeName();
            var fill = DeleteRestoreFill(factClass, predecessorsByType);
            var predecessors = predecessorsByType[factClass]
                .Where(predecessor => !hidden.Contains(predecessor.Type))
                .ToImmutableArray();
            if (fill != null)
                lines = lines.Add($"    \"{left}\" [style=filled, fillcolor={fill}]");
            if (!predecessors.Any())
            {
                if (fill == null)
                    lines = lines.Add($"    \"{left}\"");
            }
            else
            {
                lines = lines.AddRange(predecessors
                    .Select(predecessor => $"    \"{left}\" -> \"{predecessor.FactType}\" [label=\" {predecessor.Name}{Punctuation(predecessor.Cardinality)}\"]"));
            }
        }
        string graph = string.Join("\n", prefix.Concat(lines).Concat(suffix));
        return graph;
    }

    private readonly record struct CompactPredecessor(string Name, Type Type, string FactType, Cardinality Cardinality);

    private static ImmutableArray<CompactPredecessor> FactPredecessors(Type factClass)
    {
        return (
            from property in factClass.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            from predecessorType in GetFactType(property.PropertyType)
            select new CompactPredecessor(
                property.Name,
                predecessorType.Type,
                predecessorType.Type.FactTypeName(),
                predecessorType.Cardinality))
            .ToImmutableArray();
    }

    private static bool EndsWithName(Type type, string suffix)
    {
        return type.FactTypeName().EndsWith(suffix, StringComparison.Ordinal);
    }

    private static ImmutableArray<Type> SuccessorsOf(
        Type fact,
        ImmutableDictionary<Type, ImmutableArray<CompactPredecessor>> predecessorsByType)
    {
        return predecessorsByType
            .Where(pair => pair.Value.Any(predecessor => predecessor.Type == fact))
            .Select(pair => pair.Key)
            .ToImmutableArray();
    }

    private static bool SolePredecessor(
        Type fact,
        Type expected,
        ImmutableDictionary<Type, ImmutableArray<CompactPredecessor>> predecessorsByType)
    {
        var predecessors = predecessorsByType[fact];
        return predecessors.Length > 0 && predecessors.All(predecessor => predecessor.Type == expected);
    }

    private static bool TryBareDeletion(
        Type fact,
        Type deleted,
        ImmutableDictionary<Type, ImmutableArray<CompactPredecessor>> predecessorsByType)
    {
        return EndsWithName(deleted, "Deleted")
            && SolePredecessor(deleted, fact, predecessorsByType)
            && SuccessorsOf(deleted, predecessorsByType).Length == 0;
    }

    private static bool TryRestoredDeletion(
        Type fact,
        Type deleted,
        ImmutableDictionary<Type, ImmutableArray<CompactPredecessor>> predecessorsByType,
        out Type restored)
    {
        restored = null;
        if (!EndsWithName(deleted, "Deleted") || !SolePredecessor(deleted, fact, predecessorsByType))
            return false;
        var successors = SuccessorsOf(deleted, predecessorsByType);
        if (successors.Length != 1 || !EndsWithName(successors[0], "Restored"))
            return false;
        restored = successors[0];
        return SolePredecessor(restored, deleted, predecessorsByType)
            && SuccessorsOf(restored, predecessorsByType).Length == 0;
    }

    private static ImmutableHashSet<Type> HiddenDeleteRestore(
        ImmutableDictionary<Type, ImmutableArray<CompactPredecessor>> predecessorsByType)
    {
        var hidden = ImmutableHashSet.CreateBuilder<Type>();
        foreach (var fact in predecessorsByType.Keys)
        {
            foreach (var deleted in SuccessorsOf(fact, predecessorsByType))
            {
                if (TryBareDeletion(fact, deleted, predecessorsByType))
                    hidden.Add(deleted);
                else if (TryRestoredDeletion(fact, deleted, predecessorsByType, out var restored))
                {
                    hidden.Add(deleted);
                    hidden.Add(restored);
                }
            }
        }
        return hidden.ToImmutable();
    }

    private static string DeleteRestoreFill(
        Type factClass,
        ImmutableDictionary<Type, ImmutableArray<CompactPredecessor>> predecessorsByType)
    {
        var sawOrange = false;
        foreach (var deleted in SuccessorsOf(factClass, predecessorsByType))
        {
            if (TryRestoredDeletion(factClass, deleted, predecessorsByType, out _))
                return "greenyellow";
            if (TryBareDeletion(factClass, deleted, predecessorsByType))
                sawOrange = true;
        }
        return sawOrange ? "orange" : null;
    }

    private static (ImmutableList<Type> toVisit, ImmutableList<Type> visited, ImmutableList<string> lines) VisitFactType(ImmutableList<Type> toVisit, ImmutableList<Type> visited, ImmutableList<string> lines)
    {
        var factClass = toVisit.First();
        var predecessors =
            from property in factClass.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            from predecessorType in GetFactType(property.PropertyType)
            select new
            {
                Name = property.Name,
                Type = predecessorType.Type,
                FactType = predecessorType.Type.FactTypeName(),
                Cardinality = predecessorType.Cardinality
            };
        
        var left = factClass.FactTypeName();
        if (!predecessors.Any())
        {
            return (toVisit.Skip(1).ToImmutableList(), visited.Add(factClass), lines.Add($"    \"{left}\""));
        }
        var newLines = predecessors
            .Select(predecessor => $"    \"{left}\" -> \"{predecessor.FactType}\" [label=\" {predecessor.Name}{Punctuation(predecessor.Cardinality)}\"]");
        var newToVisit = predecessors
            .Select(predecessor => predecessor.Type)
            .Where(type => !toVisit.Contains(type) && !visited.Contains(type));
        
        return (toVisit.Skip(1).Concat(newToVisit).ToImmutableList(), visited.Add(factClass), lines.AddRange(newLines));
    }

    private static object Punctuation(Cardinality cardinality)
    {
        return cardinality switch
        {
            Cardinality.One => "",
            Cardinality.Many => "*",
            Cardinality.Optional => "?",
            _ => throw new ArgumentException()
        };
    }

    private static IEnumerable<(Type Type, Cardinality Cardinality)> GetFactType(Type type)
    {
        if (type.IsArray)
        {
            return GetFactType(type.GetElementType()).Select(t => (t.Type, Cardinality.Many));
        }
        else if (type.GetCustomAttributes<FactTypeAttribute>(inherit: false).Any())
        {
            return new [] { (type, Cardinality.One) };
        }
        else 
        {
            return new (Type, Cardinality)[0];
        }
    }
}
