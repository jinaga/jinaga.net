using Jinaga.Facts;
using Jinaga.Services;
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
            return JsonSerializer.Serialize(facts);
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
    }
}