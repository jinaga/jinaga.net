using System;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using Jinaga.Records;

namespace Jinaga.Http
{
    public class GraphDeserializer
    {
        public GraphDeserializer()
        {
        }

        public ImmutableList<FactRecord> Facts { get; internal set; } = ImmutableList<FactRecord>.Empty;

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
                var type = JsonSerializer.Deserialize<string>(typeLine);

                var predecessorsLine = GetLine(reader);
                var predecessors = JsonSerializer.Deserialize<JsonElement>(predecessorsLine);

                // The properties of the JSON object are roles.
                foreach (var property in predecessors.EnumerateObject())
                {
                    var role = property.Name;
                    var value = property.Value;

                    // If the value is an array, it's a predecessor list.
                    if (value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var predecessor in value.EnumerateArray())
                        {
                            // Each predecessor is represented as the integer index of the fact.
                            var index = predecessor.GetInt32();
                        }
                    }
                    else
                    {
                        // The predecessor is represented as the integer index of the fact.
                        var index = value.GetInt32();
                    }
                }

                var fieldsLine = GetLine(reader);
                var fields = JsonSerializer.Deserialize<JsonElement>(fieldsLine);

                // The properties of the JSON object are fields.
                foreach (var property in fields.EnumerateObject())
                {
                    var field = property.Name;
                    var value = property.Value;
                }

                // Skip lines until we get a blank.
                // These will be signatures.
                while (GetLine(reader) != "")
                {
                }
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
