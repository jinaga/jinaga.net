using Jinaga.Facts;
using Jinaga.Repository;
using Microsoft.AspNetCore.Html;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Web;

namespace Jinaga.Graphviz
{
    public static class Renderer
    {
        public static HtmlString RenderFacts(this JinagaClient jinagaClient, params object[] projections)
        {
            string[] prefix = new[]
            {
                "digraph {",
                "rankdir=BT",
                "node [shape=none]"
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
            return RenderGraph(dot);
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

        private static IEnumerable<string> FactToDigraph(Fact fact, bool hilight)
        {
            yield return NodeLine(fact, hilight);
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
                    yield return $"\"{fact.Reference.Hash}\" -> \"{reference.Hash}\" [label=\"{predecessor.Role}\"]";
                }
            }
        }

        private static string NodeLine(Fact fact, bool hilight)
        {
            var fieldRows = fact.Fields
                .Select(field => $"<TR><TD>{Uri.EscapeDataString(field.Name)}</TD><TD>{SerializeFieldValue(field.Value)}</TD></TR>")
                .ToArray();
            var fieldText = String.Join("", fieldRows);
            string typeRow = @$"<TR><TD COLSPAN=""2"">{Uri.EscapeDataString(fact.Reference.Type)}</TD></TR>";
            int border = hilight ? 1 : 0;
            string factLabel = @$"<TABLE BORDER=""{border}"" CELLBORDER=""1"" CELLSPACING=""0"">{typeRow}{fieldText}</TABLE>";
            return $"\"{fact.Reference.Hash}\" [label=<{factLabel}>]";
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

        public static ImmutableList<string> ListTypes(params Type[] types)
        {
            var toVisit = types
                .Where(t => t.GetCustomAttributes(typeof(FactTypeAttribute), false).Any())
                .ToImmutableList();
            var visited = ImmutableList<Type>.Empty;
            var lines = ImmutableList<string>.Empty;
            while (toVisit.Any())
            {
                (toVisit, visited, lines) = VisitFactType(toVisit, visited, lines);
            }
            return lines;
        }

        public static HtmlString RenderTypes(params Type[] types)
        {
            string[] prefix = new[]
            {
                "digraph {",
                "rankdir=BT",
            };
            string[] suffix = new[]
            {
                "}"
            };
            var toVisit = types
                .Where(t => t.GetCustomAttributes(typeof(FactTypeAttribute), false).Any())
                .ToImmutableList();
            var visited = ImmutableList<Type>.Empty;
            var lines = ImmutableList<string>.Empty;
            while (toVisit.Any())
            {
                (toVisit, visited, lines) = VisitFactType(toVisit, visited, lines);
            }
            string graph = string.Join("\n", prefix.Concat(lines).Concat(suffix));
            return RenderGraph(graph);
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
                    Type = property.PropertyType,
                    FactType = property.PropertyType.FactTypeName()
                };
            
            var left = factClass.FactTypeName();
            var newLines = predecessors
                .Select(predecessor => $"\"{left}\" -> \"{predecessor.FactType}\" [label=\"{predecessor.Name}\"]");
            var newToVisit = predecessors
                .Select(predecessor => predecessor.Type)
                .Where(type => !toVisit.Contains(type) && !visited.Contains(type));
            
            return (toVisit.Skip(1).Concat(newToVisit).ToImmutableList(), visited.Add(factClass), lines.AddRange(newLines));
        }

        private static IEnumerable<string> FactClassToDigraph(Type factClass)
        {
            var left = factClass.FactTypeName();
            var properties = factClass.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var predecessorList =
                from property in properties
                let role = property.Name
                from right in GetFactType(property.PropertyType)
                select $"{left} -> {right} [label=\"{role}\"]";
            return predecessorList;
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

        private static HtmlString RenderGraph(string graph)
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "dot";
                process.StartInfo.Arguments = "-Tsvg";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                process.StandardInput.Write(graph);
                process.StandardInput.Close();
                string svg = process.StandardOutput.ReadToEnd();

                process.WaitForExit();
                return new HtmlString(svg);
            }
        }
    }
}
