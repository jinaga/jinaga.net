using Jinaga.Facts;
using Jinaga.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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
        /// <returns>A JSON string representing all facts in the store</returns>
        public async Task<string> ExportFactsToJson()
        {
            var facts = await store.GetAllFacts();
            var json = new StringBuilder();
            json.Append("[");
            var firstFact = true;

            foreach (var fact in facts)
            {
                if (firstFact)
                {
                    json.AppendLine();
                }
                else
                {
                    json.AppendLine(",");
                }
                firstFact = false;

                json.AppendLine("    {");
                json.AppendLine($"        \"hash\": \"{fact.Reference.Hash}\",");
                json.AppendLine($"        \"type\": \"{fact.Reference.Type}\",");
                json.Append("        \"predecessors\": {");

                var firstPredecessor = true;
                foreach (var predecessor in fact.Predecessors)
                {
                    if (firstPredecessor)
                    {
                        json.AppendLine();
                    }
                    else
                    {
                        json.AppendLine(",");
                    }
                    firstPredecessor = false;

                    if (predecessor is PredecessorSingle predecessorSingle)
                    {
                        json.AppendLine($"            \"{predecessorSingle.Role}\": {{");
                        json.AppendLine($"                \"hash\": \"{predecessorSingle.Reference.Hash}\",");
                        json.AppendLine($"                \"type\": \"{predecessorSingle.Reference.Type}\"");
                        json.Append("            }");
                    }
                    else if (predecessor is PredecessorMultiple predecessorMultiple)
                    {
                        json.Append($"            \"{predecessorMultiple.Role}\": [");

                        var firstReference = true;
                        foreach (var reference in predecessorMultiple.References)
                        {
                            if (firstReference)
                            {
                                json.AppendLine();
                            }
                            else
                            {
                                json.AppendLine(",");
                            }
                            firstReference = false;

                            json.AppendLine("                {");
                            json.AppendLine($"                    \"hash\": \"{reference.Hash}\",");
                            json.AppendLine($"                    \"type\": \"{reference.Type}\"");
                            json.Append("                }");
                        }

                        if (firstReference)
                        {
                            json.Append("]");
                        }
                        else
                        {
                            json.AppendLine();
                            json.Append("            ]");
                        }
                    }
                }

                if (firstPredecessor)
                {
                    json.AppendLine("},");
                }
                else
                {
                    json.AppendLine();
                    json.AppendLine("        },");
                }

                json.Append("        \"fields\": {");

                var firstField = true;
                foreach (var field in fact.Fields)
                {
                    if (firstField)
                    {
                        json.AppendLine();
                    }
                    else
                    {
                        json.AppendLine(",");
                    }
                    firstField = false;

                    json.Append($"            \"{field.Name}\": {JsonSerialize(field.Value)}");
                }

                if (firstField)
                {
                    json.AppendLine("}");
                }
                else
                {
                    json.AppendLine();
                    json.AppendLine("        }");
                }
                json.Append("    }");
            }

            if (firstFact)
            {
                json.AppendLine("]");
            }
            else
            {
                json.AppendLine();
                json.AppendLine("]");
            }
            return json.ToString();
        }

        /// <summary>
        /// Export all facts from the store to Factual format.
        /// </summary>
        /// <returns>A Factual string representing all facts in the store</returns>
        public async Task<string> ExportFactsToFactual()
        {
            var facts = await store.GetAllFacts();
            var factual = new StringBuilder();
            var factMap = new Dictionary<FactReference, string>();

            foreach (var fact in facts)
            {
                var factName = $"f{factMap.Count + 1}";
                factMap[fact.Reference] = factName;
                factual.AppendLine($"let {factName}: {fact.Reference.Type} = {{");

                foreach (var field in fact.Fields)
                {
                    factual.AppendLine($"    {field.Name}: {JsonSerializer.Serialize(field.Value)},");
                }

                foreach (var predecessor in fact.Predecessors)
                {
                    if (predecessor is PredecessorSingle predecessorSingle)
                    {
                        var predecessorName = factMap[predecessorSingle.Reference];
                        factual.AppendLine($"    {predecessorSingle.Role}: {predecessorName},");
                    }
                    else if (predecessor is PredecessorMultiple predecessorMultiple)
                    {
                        var predecessorNames = predecessorMultiple.References.Select(p => factMap[p]);
                        factual.AppendLine($"    {predecessorMultiple.Role}: [{string.Join(", ", predecessorNames)}],");
                    }
                }

                factual.AppendLine("}");
                factual.AppendLine();
            }

            return factual.ToString();
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