using Jinaga.Identity;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Jinaga.Facts
{
    public class Fact
    {
        public static Fact Create(string type, ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            predecessors = predecessors
                .Where(predecessor => predecessor != null)
                .ToImmutableList();
            var reference = new FactReference(type, ComputeHash(fields, predecessors));
            return new Fact(reference, fields, predecessors);
        }

        private Fact(FactReference reference, ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            Reference = reference;
            Fields = fields;
            Predecessors = predecessors;
        }

        public FactReference Reference { get; }
        public ImmutableList<Field> Fields { get; }
        public ImmutableList<Predecessor> Predecessors { get; }

        public FieldValue GetFieldValue(string name)
        {
            var values = Fields
                .Where(f => f.Name == name)
                .Select(f => f.Value)
                .ToImmutableList();
            if (values.Count == 0)
            {
                return FieldValue.Undefined;
            }
            else if (values.Count > 1)
            {
                throw new ArgumentException($"The fact {Reference.Type} contained {values.Count} fields named {name}; there should only be 1");
            }
            else
            {
                return values.Single();
            }
        }

        public FactReference? GetPredecessorSingle(string role)
        {
            var references = Predecessors
                .Where(p => p.Role == role)
                .OfType<PredecessorSingle>()
                .Select(p => p.Reference)
                .ToImmutableList();
            if (references.Count == 0)
            {
                return null;
            }
            else if (references.Count > 1)
            {
                return null;
            }
            else
            {
                return references.Single();
            }
        }

        public ImmutableList<FactReference> GetPredecessorMultiple(string role)
        {
            var references = Predecessors
                .Where(p => p.Role == role)
                .OfType<PredecessorMultiple>()
                .SelectMany(p => p.References)
                .ToImmutableList();
            return references;
        }

        public ImmutableList<FactReference> GetPredecessors(string role)
        {
            var references = Predecessors
                .Where(p => p.Role == role)
                .ToImmutableList();
            if (references.Count == 0)
            {
                throw new ArgumentException($"The fact {Reference.Type} did not contain any predecessors in role {role}");
            }
            else if (references.Count > 1)
            {
                throw new ArgumentException($"The fact {Reference.Type} contained {references.Count} predecessors in role {role}; there should only be 1");
            }
            return references.Single() switch
            {
                PredecessorSingle single => ImmutableList<FactReference>.Empty.Add(single.Reference),
                PredecessorMultiple multiple => multiple.References,
                _ => throw new InvalidOperationException("Unknown predecessor type")
            };
        }

        public IEnumerable<FactReference> GetAllPredecessorReferences()
        {
            return Predecessors
                .Where(p => p != null)
                .SelectMany(p => p switch
                {
                    PredecessorSingle single => ImmutableList<FactReference>.Empty.Add(single.Reference),
                    PredecessorMultiple multiple => multiple.References,
                    _ => throw new InvalidOperationException("Unknown predecessor type")
                });
        }

        private static string ComputeHash(ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            string json = Canonicalize(fields, predecessors);
            return IdentityUtilities.ComputeStringHash(json);
        }

        public static string Canonicalize(ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            string fieldsString = CanonicalizeFields(fields);
            string predecessorsString = CanonicalizePredecessors(predecessors);
            return $"{{\"fields\":{{{fieldsString}}},\"predecessors\":{{{predecessorsString}}}}}";
        }

        private static string CanonicalizeFields(ImmutableList<Field> fields)
        {
            var serializedFields = fields
                .OrderBy(field => field.Name, StringComparer.Ordinal)
                .Select(field => $"\"{field.Name}\":{SerializeFieldValue(field.Value)}")
                .ToArray();
            var result = String.Join(",", serializedFields);
            return result;
        }

        private static JsonSerializerOptions RelaxedStringOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static string SerializeFieldValue(FieldValue value)
        {
            switch (value)
            {
                case FieldValueString str:
                    return JsonSerializer.Serialize(str.StringValue, RelaxedStringOptions);
                case FieldValueNumber number:
                    return JsonSerializer.Serialize(number.DoubleValue);
                case FieldValueBoolean boolean:
                    return JsonSerializer.Serialize(boolean.BoolValue);
                default:
                    throw new NotImplementedException();
            }
        }

        private static string CanonicalizePredecessors(ImmutableList<Predecessor> predecessors)
        {
            var serializedPredecessors = predecessors
                .Where(predecessor => predecessor != null)
                .OrderBy(predecessor => predecessor.Role, StringComparer.Ordinal)
                .Select(predecessor => $"\"{predecessor.Role}\":{SerializePredecessor(predecessor)}")
                .ToArray();
            var result = String.Join(",", serializedPredecessors);
            return result;
        }

        private static string SerializePredecessor(Predecessor predecessor)
        {
            switch (predecessor)
            {
                case PredecessorSingle single:
                    return SerializeFactReference(single.Reference);
                case PredecessorMultiple multiple:
                    var referenceStrings = multiple
                        .References
                        .OrderBy(reference => reference.Hash, StringComparer.Ordinal)
                        .ThenBy(references => references.Type, StringComparer.Ordinal)
                        .Select(reference => SerializeFactReference(reference))
                        .ToArray();
                    return $"[{String.Join(",", referenceStrings)}]";
                default:
                    throw new NotImplementedException();
            }
        }

        private static string SerializeFactReference(FactReference reference)
        {
            string serializedType = JsonSerializer.Serialize(reference.Type);
            return $"{{\"hash\":\"{reference.Hash}\",\"type\":{serializedType}}}";
        }
    }
}