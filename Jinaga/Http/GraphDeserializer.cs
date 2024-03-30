using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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
                var predecessors = predecessorsElement.EnumerateObject()
                    .Select(CreatePredecessor)
                    .ToImmutableList();

                var fieldsLine = GetLine(reader);
                var fieldsElement = JsonSerializer.Deserialize<JsonElement>(fieldsLine);

                // The properties of the JSON object are fields.
                var fields = fieldsElement.EnumerateObject()
                    .Select(CreateField)
                    .ToImmutableList();

                // Skip lines until we get a blank.
                // These will be signatures.
                while (GetLine(reader) != "")
                {
                }

                // Add a fact to the graph.
                var fact = Fact.Create(type, fields, predecessors);
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

        private Predecessor CreatePredecessor(JsonProperty property)
        {
            var role = property.Name;
            var value = property.Value;

            if (value.ValueKind == JsonValueKind.Array)
            {
                var factReferences = value.EnumerateArray()
                    .Select(predecessor => Graph.FactReferences[predecessor.GetInt32()])
                    .ToImmutableList();

                return new PredecessorMultiple(role, factReferences);
            }
            else
            {
                var factReference = Graph.FactReferences[value.GetInt32()];
                return new PredecessorSingle(role, factReference);
            }
        }

        private Field CreateField(JsonProperty property)
        {
            return new Field(property.Name, CreateFieldValue(property.Value));
        }

        private FieldValue CreateFieldValue(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return new FieldValueString(value.GetString());
                case JsonValueKind.Number:
                    return new FieldValueNumber(value.GetDouble());
                case JsonValueKind.True:
                    return new FieldValueBoolean(true);
                case JsonValueKind.False:
                    return new FieldValueBoolean(false);
                case JsonValueKind.Null:
                    return new FieldValueNull();
                default:
                    throw new Exception("Unexpected field value kind: " + value.ValueKind);
            }
        }
    }
}
