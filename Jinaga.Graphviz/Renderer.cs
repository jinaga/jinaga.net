using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Jinaga.Facts;
using Jinaga.Serialization;
using Microsoft.AspNetCore.Html;

namespace Jinaga.Graphviz
{
    public static class Renderer
    {
        public static HtmlString RenderFacts<T>(IEnumerable<T> facts)
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
            var collector = new Collector(SerializerCache.Empty);
            var references = new List<FactReference>();
            foreach (var fact in facts)
            {
                var reference = collector.Serialize(fact);
                references.Add(reference);
            }
            var body = collector.Graph.FactReferences
                .SelectMany(reference => FactToDigraph(collector.Graph.GetFact(reference), references.Contains(reference)));
            string graph = string.Join("\n", prefix.Concat(body).Concat(suffix));
            return RenderGraph(graph);
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
                .Select(field => $"<TR><TD>{Uri.EscapeUriString(field.Name)}</TD><TD>{SerializeFieldValue(field.Value)}</TD></TR>")
                .ToArray();
            var fieldText = String.Join("", fieldRows);
            string typeRow = @$"<TR><TD COLSPAN=""2"">{Uri.EscapeUriString(fact.Reference.Type)}</TD></TR>";
            int border = hilight ? 1 : 0;
            string factLabel = @$"<TABLE BORDER=""{border}"" CELLBORDER=""1"" CELLSPACING=""0"">{typeRow}{fieldText}</TABLE>";
            return $"\"{fact.Reference.Hash}\" [label=<{factLabel}>]";
        }
        
        private static string SerializeFieldValue(FieldValue value)
        {
            return value switch
            {
                FieldValueString str => JsonSerializer.Serialize(str.StringValue),
                FieldValueNumber number => JsonSerializer.Serialize(number.DoubleValue),
                FieldValueBoolean b => JsonSerializer.Serialize(b.BoolValue),
                _ => throw new NotImplementedException()
            };
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
