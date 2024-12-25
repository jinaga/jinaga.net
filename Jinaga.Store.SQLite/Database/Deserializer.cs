using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Jinaga.Facts;

namespace Jinaga.Store.SQLite.Database
{
    internal static class Deserializer
    {
        public static IEnumerable<FactEnvelope> Deserialize(this IEnumerable<FactWithIdAndSignatureFromDb> factsFromDb)
        {
            FactEnvelope envelope = null;
            int factId = 0;
            foreach (var FactFromDb in factsFromDb)
            {
                if (factId != 0 && factId != FactFromDb.fact_id)
                {
                    // We've reached a new fact. Return the previous one.
                    yield return envelope;
                    envelope = null;
                    factId = 0;
                }

                if (envelope == null)
                {
                    envelope = LoadEnvelope(FactFromDb);
                    factId = FactFromDb.fact_id;
                }

                // Add the signature to the envelope.
                if (FactFromDb.public_key != null)
                {
                    var signature = new FactSignature(FactFromDb.public_key, FactFromDb.signature);
                    envelope = envelope.AddSignature(signature);
                }
            }
            if (envelope != null)
            {
                yield return envelope;
            }
        }

        public static FactEnvelope LoadEnvelope(FactWithIdAndSignatureFromDb FactFromDb)
        {
            FactEnvelope envelope;
            ImmutableList<Field> fields = ImmutableList<Field>.Empty;
            ImmutableList<Predecessor> predecessors = ImmutableList<Predecessor>.Empty;

            using (JsonDocument document = JsonDocument.Parse(FactFromDb.data))
            {
                JsonElement root = document.RootElement;

                JsonElement fieldsElement = root.GetProperty("fields");
                foreach (var field in fieldsElement.EnumerateObject())
                {
                    switch (field.Value.ValueKind)
                    {
                        case JsonValueKind.String:
                            fields = fields.Add(new Field(field.Name, new FieldValueString(field.Value.GetString())));
                            break;
                        case JsonValueKind.Number:
                            fields = fields.Add(new Field(field.Name, new FieldValueNumber(field.Value.GetDouble())));
                            break;
                        case JsonValueKind.True:
                        case JsonValueKind.False:
                            fields = fields.Add(new Field(field.Name, new FieldValueBoolean(field.Value.GetBoolean())));
                            break;
                        case JsonValueKind.Null:
                            fields = fields.Add(new Field(field.Name, FieldValue.Null));
                            break;
                    }
                }

                string hash;
                string type;
                JsonElement predecessorsElement = root.GetProperty("predecessors");
                foreach (var predecessor in predecessorsElement.EnumerateObject())
                {
                    switch (predecessor.Value.ValueKind)
                    {
                        case JsonValueKind.Object:
                            hash = predecessor.Value.GetProperty("hash").GetString();
                            type = predecessor.Value.GetProperty("type").GetString();
                            predecessors = predecessors.Add(new PredecessorSingle(predecessor.Name, new FactReference(type, hash)));
                            break;
                        case JsonValueKind.Array:
                            ImmutableList<FactReference> factReferences = ImmutableList<FactReference>.Empty;
                            foreach (var factReference in predecessor.Value.EnumerateArray())
                            {
                                hash = factReference.GetProperty("hash").GetString();
                                type = factReference.GetProperty("type").GetString();
                                factReferences = factReferences.Add(new FactReference(type, hash));
                            }
                            predecessors = predecessors.Add(new PredecessorMultiple(predecessor.Name, factReferences));
                            break;
                    }
                }
            }

            var fact = Fact.Create(FactFromDb.name, fields, predecessors);
            envelope = new FactEnvelope(fact, ImmutableList<FactSignature>.Empty);
            return envelope;
        }
    }
}
