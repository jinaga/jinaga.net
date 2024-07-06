using Jinaga.Facts;
using Jinaga.Repository;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;

namespace Jinaga.Notebooks.Dot;

public static class JinagaClientExtensions
{
    public static string RenderFacts(this JinagaClient jinagaClient, params object[] projections)
    {
        string[] prefix = new[]
        {
            "digraph {",
            "    rankdir=BT",
            "    node [shape=none]"
        };
        string[] suffix = new[]
        {
            "}"
        };
        var graph = FactGraph.Empty;
        var references = new List<FactReference>();
        foreach (var fact in projections.SelectMany(projection => GetFacts(projection, 5)))
        {
            var factGraph = jinagaClient.Graph(fact);
            graph = graph.AddGraph(factGraph);
            var reference = factGraph.Last;
            references.Add(reference);
        }
        var body = graph.FactReferences
            .SelectMany(reference => FactToDigraph(graph.GetFact(reference), references.Contains(reference)));
        string dot = string.Join("\n", prefix.Concat(body).Concat(suffix));
        return dot;
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
        else
        {
            if (type.IsAssignableTo(typeof(IEnumerable)) && type != typeof(string))
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

    private static IEnumerable<string> FactToDigraph(Fact fact, bool highlight)
    {
        yield return NodeLine(fact, highlight);
        foreach (var predecessor in fact.Predecessors)
        {
            var references = predecessor switch
            {
                PredecessorSingle single => new[] { single.Reference },
                PredecessorMultiple multiple => multiple.References,
                _ => Enumerable.Empty<FactReference>()
            };
            foreach (var reference in references)
            {
                yield return $"    \"{fact.Reference.Hash}\" -> \"{reference.Hash}\" [label=\" {predecessor.Role}\"]";
            }
        }
    }

    private static string NodeLine(Fact fact, bool highlight)
    {
        var fieldRows = fact.Fields
            .Select(field => $"<TR><TD>{Uri.EscapeDataString(field.Name)}</TD><TD>{SerializeFieldValue(field.Value)}</TD></TR>")
            .ToArray();
        var fieldText = String.Join("", fieldRows);
        string typeRow = @$"<TR><TD COLSPAN=""2"">{Uri.EscapeDataString(fact.Reference.Type)}</TD></TR>";
        int border = highlight ? 1 : 0;
        string factLabel = @$"<TABLE BORDER=""{border}"" CELLBORDER=""1"" CELLSPACING=""0"">{typeRow}{fieldText}</TABLE>";
        return $"    \"{fact.Reference.Hash}\" [label=<{factLabel}>]";
    }
    
    private static string SerializeFieldValue(FieldValue value)
    {
        return value switch
        {
            FieldValueString str => HttpUtility.HtmlEncode(Limit(str.StringValue)),
            FieldValueNumber number => number.DoubleValue.ToString(),
            FieldValueBoolean b => b.BoolValue ? "true" : "false",
            FieldValueNull _ => "null",
            _ => throw new NotImplementedException()
        };
    }

    private static string Limit(string stringValue)
    {
        return stringValue != null && stringValue.Length > 20
            ? $"{stringValue[..20]}..."
            : stringValue;
    }
}
