using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            foreach (var fact in facts)
            {
                collector.Serialize(fact);
            }
            var body = collector.Graph.FactReferences
                .SelectMany(reference => FactToDigraph(collector.Graph.GetFact(reference)));
            string graph = string.Join("\n", prefix.Concat(body).Concat(suffix));
            return RenderGraph(graph);
        }

        private static IEnumerable<string> FactToDigraph(Fact fact)
        {
            yield return NodeLine(fact);
        }

        private static string NodeLine(Fact fact)
        {
            string factRows = @$"<TR><TD COLSPAN=""2"">{Uri.EscapeUriString(fact.Reference.Type)}</TD></TR>";
            string factLabel = @$"<TABLE BORDER=""0"" CELLBORDER=""1"" CELLSPACING=""0"">{factRows}</TABLE>";
            return $"\"{fact.Reference.Hash}\" [label=<{factLabel}>]";
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
