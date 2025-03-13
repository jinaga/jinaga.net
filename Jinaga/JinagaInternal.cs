using Jinaga.Facts;
using Jinaga.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;

namespace Jinaga
{
    /// <summary>
    /// Provides internal methods for exporting the SQLite store contents.
    /// </summary>
    public class JinagaInternal
    {
        private readonly IStore store;

        public JinagaInternal(IStore store)
        {
            this.store = store;
        }

        /// <summary>
        /// Export all facts from the store to JSON format.
        /// </summary>
        /// <param name="stream">The stream to write the JSON data to</param>
        public async Task ExportFactsToJson(Stream stream)
        {
            var facts = await store.GetAllFacts();
            var firstFact = true;
            var chunk = new StringBuilder();
            chunk.Append("[");

            foreach (var fact in facts)
            {
                if (firstFact)
                {
                    chunk.AppendLine();
                }
                else
                {
                    chunk.AppendLine(",");
                }
                firstFact = false;

                chunk.AppendLine("    {");
                chunk.AppendLine($"        \"hash\": \"{fact.Reference.Hash}\",");
                chunk.AppendLine($"        \"type\": \"{fact.Reference.Type}\",");
                chunk.Append("        \"predecessors\": {");

                var firstPredecessor = true;
                foreach (var predecessor in fact.Predecessors)
                {
                    if (firstPredecessor)
                    {
                        chunk.AppendLine();
                    }
                    else
                    {
                        chunk.AppendLine(",");
                    }
                    firstPredecessor = false;

                    if (predecessor is PredecessorSingle predecessorSingle)
                    {
                        chunk.AppendLine($"            \"{predecessorSingle.Role}\": {{");
                        chunk.AppendLine($"                \"hash\": \"{predecessorSingle.Reference.Hash}\",");
                        chunk.AppendLine($"                \"type\": \"{predecessorSingle.Reference.Type}\"");
                        chunk.Append("            }");
                    }
                    else if (predecessor is PredecessorMultiple predecessorMultiple)
                    {
                        chunk.Append($"            \"{predecessorMultiple.Role}\": [");

                        var firstReference = true;
                        foreach (var reference in predecessorMultiple.References)
                        {
                            if (firstReference)
                            {
                                chunk.AppendLine();
                            }
                            else
                            {
                                chunk.AppendLine(",");
                            }
                            firstReference = false;

                            chunk.AppendLine("                {");
                            chunk.AppendLine($"                    \"hash\": \"{reference.Hash}\",");
                            chunk.AppendLine($"                    \"type\": \"{reference.Type}\"");
                            chunk.Append("                }");
                        }

                        if (firstReference)
                        {
                            chunk.Append("]");
                        }
                        else
                        {
                            chunk.AppendLine();
                            chunk.Append("            ]");
                        }
                    }
                }

                if (firstPredecessor)
                {
                    chunk.AppendLine("},");
                }
                else
                {
                    chunk.AppendLine();
                    chunk.AppendLine("        },");
                }

                chunk.Append("        \"fields\": {");

                var firstField = true;
                foreach (var field in fact.Fields)
                {
                    if (firstField)
                    {
                        chunk.AppendLine();
                    }
                    else
                    {
                        chunk.AppendLine(",");
                    }
                    firstField = false;

                    chunk.Append($"            \"{field.Name}\": {JsonSerialize(field.Value)}");
                }

                if (firstField)
                {
                    chunk.AppendLine("}");
                }
                else
                {
                    chunk.AppendLine();
                    chunk.AppendLine("        }");
                }
                chunk.Append("    }");

                await WriteChunkToStream(stream, chunk.ToString());
                chunk.Clear();
            }

            if (firstFact)
            {
                await WriteChunkToStream(stream, "]\n");
            }
            else
            {
                await WriteChunkToStream(stream, "\n]\n");
            }
        }

        /// <summary>
        /// Export all facts from the store to Factual format.
        /// </summary>
        /// <param name="stream">The stream to write the Factual data to</param>
        public async Task ExportFactsToFactual(Stream stream)
        {
            var facts = await store.GetAllFacts();
            var factMap = new Dictionary<FactReference, string>();
            var buffer = new List<(Fact fact, string factName)>();

            // First pass: collect all facts and assign names
            foreach (var fact in facts)
            {
                var factName = $"f{factMap.Count + 1}";
                factMap[fact.Reference] = factName;
                buffer.Add((fact, factName));
            }

            // Second pass: output facts now that we have all references
            var chunk = new StringBuilder();
            foreach (var (fact, factName) in buffer)
            {
                chunk.Append($"let {factName}: {fact.Reference.Type} = {{");
                var first = true;

                foreach (var field in fact.Fields)
                {
                    if (first)
                    {
                        chunk.AppendLine();
                    }
                    else
                    {
                        chunk.AppendLine(",");
                    }
                    first = false;
                    chunk.Append($"    {field.Name}: {JsonSerialize(field.Value)}");
                }

                foreach (var predecessor in fact.Predecessors)
                {
                    if (first)
                    {
                        chunk.AppendLine();
                    }
                    else
                    {
                        chunk.AppendLine(",");
                    }
                    first = false;
                    if (predecessor is PredecessorSingle predecessorSingle)
                    {
                        var predecessorName = factMap[predecessorSingle.Reference];
                        chunk.Append($"    {predecessorSingle.Role}: {predecessorName}");
                    }
                    else if (predecessor is PredecessorMultiple predecessorMultiple)
                    {
                        var predecessorNames = predecessorMultiple.References.Select(p => factMap[p]);
                        chunk.Append($"    {predecessorMultiple.Role}: [{string.Join(", ", predecessorNames)}]");
                    }
                }

                if (first)
                {
                    chunk.AppendLine("}");
                }
                else
                {
                    chunk.AppendLine();
                    chunk.AppendLine("}");
                }
                chunk.AppendLine();

                await WriteChunkToStream(stream, chunk.ToString());
                chunk.Clear();
            }
        }

        private async Task WriteChunkToStream(Stream stream, string chunk)
        {
            var buffer = Encoding.UTF8.GetBytes(chunk);
            await stream.WriteAsync(buffer, 0, buffer.Length);
        }

        private string JsonSerialize(FieldValue value)
        {
            if (value is FieldValueString stringValue)
            {
                return JsonSerializer.Serialize(stringValue.StringValue);
            }
            else if (value is FieldValueNumber numberValue)
            {
                return JsonSerializer.Serialize(numberValue.DoubleValue);
            }
            else if (value is FieldValueBoolean booleanValue)
            {
                return JsonSerializer.Serialize(booleanValue.BoolValue);
            }
            else if (value is FieldValueNull)
            {
                return "null";
            }
            else
            {
                throw new InvalidOperationException("Unknown field value type");
            }
        }
    }
}
