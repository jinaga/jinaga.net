using System.Collections.Immutable;
using System.Text;

namespace Jinaga.Notebooks.Test.Dot;

public sealed record DotEdge(string From, string To, ImmutableDictionary<string, string> Attributes);

/// <summary>
/// The graph a DOT document describes, independent of how it is spelled.
/// Statement order, whitespace, and whether a node's attributes are given on one
/// line or several do not change the parsed graph.
///
/// Parses only the subset of DOT that the Jinaga.Notebooks renderers emit, one
/// statement per line. Anything else throws, so a change in the renderers' output
/// shows up as a parse failure rather than as a silently misread graph.
/// </summary>
public sealed class DotGraph
{
    public ImmutableDictionary<string, string> GraphAttributes { get; }
    public ImmutableDictionary<string, string> NodeDefaults { get; }
    public ImmutableDictionary<string, ImmutableDictionary<string, string>> Nodes { get; }
    public ImmutableList<DotEdge> Edges { get; }

    private DotGraph(
        ImmutableDictionary<string, string> graphAttributes,
        ImmutableDictionary<string, string> nodeDefaults,
        ImmutableDictionary<string, ImmutableDictionary<string, string>> nodes,
        ImmutableList<DotEdge> edges)
    {
        GraphAttributes = graphAttributes;
        NodeDefaults = nodeDefaults;
        Nodes = nodes;
        Edges = edges;
    }

    public static DotGraph Parse(string dot)
    {
        var lines = dot.Split('\n').Select(line => line.Trim()).ToList();
        if (lines.FirstOrDefault() != "digraph {" || lines.LastOrDefault() != "}")
        {
            throw new FormatException($"Expected a digraph:\n{dot}");
        }

        var graphAttributes = ImmutableDictionary<string, string>.Empty;
        var nodeDefaults = ImmutableDictionary<string, string>.Empty;
        var nodes = ImmutableDictionary<string, ImmutableDictionary<string, string>>.Empty;
        var edges = ImmutableList<DotEdge>.Empty;

        foreach (var line in lines.Skip(1).Take(lines.Count - 2).Where(line => line.Length > 0))
        {
            var scanner = new Scanner(line);
            if (scanner.Peek == '"')
            {
                var from = scanner.ReadQuoted();
                nodes = DeclareNode(nodes, from);
                if (scanner.TryRead("->"))
                {
                    var to = scanner.ReadQuoted();
                    nodes = DeclareNode(nodes, to);
                    edges = edges.Add(new DotEdge(from, to, scanner.ReadAttributeList()));
                }
                else
                {
                    nodes = nodes.SetItem(from, nodes[from].SetItems(scanner.ReadAttributeList()));
                }
            }
            else if (scanner.TryRead("node"))
            {
                nodeDefaults = nodeDefaults.SetItems(scanner.ReadAttributeList());
            }
            else
            {
                var (key, value) = scanner.ReadAttribute();
                graphAttributes = graphAttributes.SetItem(key, value);
            }
            scanner.ExpectEnd();
        }

        return new DotGraph(graphAttributes, nodeDefaults, nodes, edges);
    }

    private static ImmutableDictionary<string, ImmutableDictionary<string, string>> DeclareNode(
        ImmutableDictionary<string, ImmutableDictionary<string, string>> nodes,
        string name)
    {
        return nodes.ContainsKey(name)
            ? nodes
            : nodes.Add(name, ImmutableDictionary<string, string>.Empty);
    }

    private sealed class Scanner
    {
        private readonly string line;
        private int position;

        public Scanner(string line)
        {
            this.line = line;
        }

        public char Peek => position < line.Length ? line[position] : '\0';

        public bool TryRead(string token)
        {
            SkipWhitespace();
            if (string.CompareOrdinal(line, position, token, 0, token.Length) != 0)
            {
                return false;
            }
            position += token.Length;
            return true;
        }

        public string ReadQuoted()
        {
            SkipWhitespace();
            Expect('"');
            var builder = new StringBuilder();
            while (Peek != '"')
            {
                if (Peek == '\0')
                {
                    throw Error("unterminated string");
                }
                if (Peek == '\\' && position + 1 < line.Length &&
                    (line[position + 1] == '"' || line[position + 1] == '\\'))
                {
                    position++;
                }
                builder.Append(line[position++]);
            }
            Expect('"');
            return builder.ToString();
        }

        public ImmutableDictionary<string, string> ReadAttributeList()
        {
            var attributes = ImmutableDictionary<string, string>.Empty;
            if (!TryRead("["))
            {
                return attributes;
            }
            while (true)
            {
                SkipWhitespace();
                if (TryRead("]"))
                {
                    return attributes;
                }
                var (key, value) = ReadAttribute();
                attributes = attributes.SetItem(key, value);
                TryRead(",");
            }
        }

        public (string key, string value) ReadAttribute()
        {
            SkipWhitespace();
            var key = ReadBare();
            if (!TryRead("="))
            {
                throw Error("expected '='");
            }
            SkipWhitespace();
            var value = Peek switch
            {
                '"' => ReadQuoted(),
                '<' => ReadHtml(),
                _ => ReadBare()
            };
            return (key, value);
        }

        public void ExpectEnd()
        {
            SkipWhitespace();
            if (position != line.Length)
            {
                throw Error("unexpected text");
            }
        }

        private string ReadHtml()
        {
            Expect('<');
            var start = position;
            var depth = 1;
            while (depth > 0)
            {
                if (Peek == '\0')
                {
                    throw Error("unterminated HTML label");
                }
                if (Peek == '<') depth++;
                if (Peek == '>') depth--;
                position++;
            }
            return line.Substring(start, position - start - 1);
        }

        private string ReadBare()
        {
            var start = position;
            while (Peek != '\0' && (char.IsLetterOrDigit(Peek) || Peek == '_' || Peek == '.'))
            {
                position++;
            }
            if (position == start)
            {
                throw Error("expected an identifier");
            }
            return line.Substring(start, position - start);
        }

        private void Expect(char c)
        {
            if (Peek != c)
            {
                throw Error($"expected '{c}'");
            }
            position++;
        }

        private void SkipWhitespace()
        {
            while (Peek == ' ' || Peek == '\t' || Peek == '\r')
            {
                position++;
            }
        }

        private FormatException Error(string message)
        {
            return new FormatException($"{message} at column {position + 1} of: {line}");
        }
    }
}
