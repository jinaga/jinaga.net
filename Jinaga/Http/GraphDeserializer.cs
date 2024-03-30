using System;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using Jinaga.Facts;

namespace Jinaga.Http
{
    public class GraphDeserializer
    {
        public FactGraph Graph { get; internal set; } = FactGraph.Empty;

        public void Deserialize(Stream stream)
        {
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                var typeLine = GetLine(reader);
                // If the line is empty, then we are done.
                if (typeLine == "")
                {
                    break;
                }
                // If the line starts with PK, then it is a public key.
                // Skip lines until the next blank.
                if (typeLine.StartsWith("PK"))
                {
                    while (GetLine(reader) != "")
                    {
                    }
                    continue;
                }
                var type = JsonSerializer.Deserialize<string>(typeLine);

                var predecessorsLine = GetLine(reader);
                var predecessorsElement = JsonSerializer.Deserialize<JsonElement>(predecessorsLine);

                // The properties of the JSON object are roles.
                var predecessors = ImmutableList<Predecessor>.Empty;
                foreach (var property in predecessorsElement.EnumerateObject())
                {
                    var role = property.Name;
                    var value = property.Value;

                    // If the value is an array, it's a predecessor list.
                    if (value.ValueKind == JsonValueKind.Array)
                    {
                        var factReferences = ImmutableList<FactReference>.Empty;
                        foreach (var predecessor in value.EnumerateArray())
                        {
                            // Each predecessor is represented as the integer index of the fact.
                            var index = predecessor.GetInt32();
                            var factReference = Graph.FactReferences[index];
                            factReferences = factReferences.Add(factReference);
                        }
                        predecessors = predecessors.Add(new PredecessorMultiple(role, factReferences));
                    }
                    else
                    {
                        // The predecessor is represented as the integer index of the fact.
                        var index = value.GetInt32();
                        var factReference = Graph.FactReferences[index];
                        predecessors = predecessors.Add(new PredecessorSingle(role, factReference));
                    }
                }

                var fieldsLine = GetLine(reader);
                var fieldsElement = JsonSerializer.Deserialize<JsonElement>(fieldsLine);

                // The properties of the JSON object are fields.
                var fields = ImmutableList<Field>.Empty;
                foreach (var property in fieldsElement.EnumerateObject())
                {
                    var name = property.Name;
                    var value = property.Value;

                    switch (value.ValueKind)
                    {
                        case JsonValueKind.String:
                            fields = fields.Add(new Field(name, new FieldValueString(value.GetString())));
                            break;
                        case JsonValueKind.Number:
                            fields = fields.Add(new Field(name, new FieldValueNumber(value.GetDouble())));
                            break;
                        case JsonValueKind.True:
                            fields = fields.Add(new Field(name, new FieldValueBoolean(true)));
                            break;
                        case JsonValueKind.False:
                            fields = fields.Add(new Field(name, new FieldValueBoolean(false)));
                            break;
                        case JsonValueKind.Null:
                            fields = fields.Add(new Field(name, new FieldValueNull()));
                            break;
                        default:
                            throw new Exception("Unexpected field value kind: " + value.ValueKind);
                    }
                }

                // Skip lines until we get a blank.
                // These will be signatures.
                while (GetLine(reader) != "")
                {
                }

                // Add a fact to the graph.
                Fact fact = Fact.Create(type, fields, predecessors);
                Graph = Graph.Add(fact);
            }
        }

        private static string GetLine(StreamReader reader)
        {
            string line = reader.ReadLine();
            if (line == null)
            {
                throw new Exception("Unexpected end of stream.");
            }
            return line;
        }
    }
}
