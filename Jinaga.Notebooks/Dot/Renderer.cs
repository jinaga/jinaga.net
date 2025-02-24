using Jinaga.Repository;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Jinaga.Notebooks.Dot;

public static class Renderer
{
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

    private static (ImmutableList<Type> toVisit, ImmutableList<Type> visited, ImmutableList<string> lines) VisitFactType(ImmutableList<Type> toVisit, ImmutableList<Type> visited, ImmutableList<string> lines)
    {
        var factClass = toVisit.First();
        var predecessors =
            from property in factClass.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            from predecessorType in GetFactType(property.PropertyType)
            select new
            {
                Name = property.Name,
                Type = predecessorType,
                FactType = predecessorType.FactTypeName()
            };
        
        var left = factClass.FactTypeName();
        if (!predecessors.Any())
        {
            return (toVisit.Skip(1).ToImmutableList(), visited.Add(factClass), lines.Add($"    \"{left}\""));
        }
        var newLines = predecessors
            .Select(predecessor => $"    \"{left}\" -> \"{predecessor.FactType}\" [label=\" {predecessor.Name}\"]");
        var newToVisit = predecessors
            .Select(predecessor => predecessor.Type)
            .Where(type => !toVisit.Contains(type) && !visited.Contains(type));
        
        return (toVisit.Skip(1).Concat(newToVisit).ToImmutableList(), visited.Add(factClass), lines.AddRange(newLines));
    }

    private static IEnumerable<Type> GetFactType(Type type)
    {
        if (type.IsArray)
        {
            return GetFactType(type.GetElementType());
        }
        else if (type.GetCustomAttributes<FactTypeAttribute>(inherit: false).Any())
        {
            return new [] { type };
        }
        else 
        {
            return new Type[0];
        }
    }
}
