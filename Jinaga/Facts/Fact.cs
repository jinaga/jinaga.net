using System;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Jinaga.Facts
{
    public class Fact
    {
        public static Fact Create(string type, ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            var reference = new FactReference(type, ComputeHash(fields, predecessors));
            return new Fact(reference, fields, predecessors);
        }

        public Fact(FactReference reference, ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            Reference = reference;
            Fields = fields;
            Predecessors = predecessors;
        }

        public FactReference Reference { get; }
        public ImmutableList<Field> Fields { get; }
        public ImmutableList<Predecessor> Predecessors { get; }

        public static string ComputeHash(ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            string json = Canonicalize(fields, predecessors);
            var bytes = Encoding.UTF8.GetBytes(json);
            using var hashAlgorithm = HashAlgorithm.Create("SHA-512");
            var hashBytes = hashAlgorithm.ComputeHash(bytes);
            var hashString = Convert.ToBase64String(hashBytes);
            return hashString;
        }

        private static string Canonicalize(ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
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

        private static string SerializeFieldValue(FieldValue value)
        {
            switch (value)
            {
                case FieldValueString str:
                    return JsonSerializer.Serialize(str.StringValue);
                case FieldValueNumber number:
                    return JsonSerializer.Serialize(number.DoubleValue);
                default:
                    throw new NotImplementedException();
            }
        }

        private static string CanonicalizePredecessors(ImmutableList<Predecessor> predecessors)
        {
            var serializedPredecessors = predecessors
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