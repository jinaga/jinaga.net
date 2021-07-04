using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using System;
using System.Collections.Immutable;
using Jinaga.Parsers;
using System.Linq;
using System.Text.Json;

namespace Jinaga.Facts
{
    public class FactSerializer
    {
        public static ImmutableList<Fact> Serialize(object runtimeFact)
        {
            var collector = new Collector();
            var reference = collector.Serialize(runtimeFact);
            return collector.Facts;
        }

        private class Collector
        {
            public ImmutableList<Fact> Facts { get; set; } = ImmutableList<Fact>.Empty;

            public FactReference Serialize(object runtimeFact)
            {
                var runtimeType = runtimeFact.GetType();
                var properties = runtimeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var fields = properties
                    .Where(property => IsField(property))
                    .Select(property => SerializeField(property, runtimeFact))
                    .ToImmutableList();
                var predecessors = properties
                    .Where(property => !IsField(property) && !IsCondition(property))
                    .Select(property => SerializePredecessor(property, runtimeFact))
                    .ToImmutableList();
                var reference = new FactReference(runtimeType.FactTypeName(), ComputeHash(fields, predecessors));
                Facts = Facts.Add(new Fact(reference, fields, predecessors));
                return reference;
            }

            private static bool IsField(PropertyInfo property)
            {
                return
                    property.PropertyType == typeof(string) ||
                    property.PropertyType == typeof(DateTime) ||
                    property.PropertyType == typeof(int) ||
                    property.PropertyType == typeof(float) ||
                    property.PropertyType == typeof(double) ||
                    property.PropertyType == typeof(bool);
            }

            private bool IsCondition(PropertyInfo property)
            {
                return property.PropertyType == typeof(Condition);
            }

            private static Field SerializeField(PropertyInfo property, object runtimeFact)
            {
                object propertyValue = property.GetValue(runtimeFact);
                var value =
                    property.PropertyType == typeof(string)
                        ? FieldValue.Value((string)propertyValue)
                    : property.PropertyType == typeof(DateTime)
                        ? FieldValue.Value((DateTime)propertyValue)
                    : property.PropertyType == typeof(int)
                        ? FieldValue.Value((int)propertyValue)
                    : property.PropertyType == typeof(float)
                        ? FieldValue.Value((float)propertyValue)
                    : property.PropertyType == typeof(double)
                        ? FieldValue.Value((double)propertyValue)
                    : property.PropertyType == typeof(bool)
                        ? FieldValue.Value((bool)propertyValue)
                    : throw new ArgumentException($"Unsupported field type {property.PropertyType.Name} in {property.DeclaringType.Name}.{property.Name}");
                return new Field(property.Name, value);
            }

            private Predecessor SerializePredecessor(PropertyInfo property, object runtimeFact)
            {
                string role = property.Name;
                var reference = Serialize(property.GetValue(runtimeFact));
                return new PredecessorSingle(role, reference);
            }

            private string ComputeHash(ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
            {
                string json = Canonicalize(fields, predecessors);
                var bytes = Encoding.UTF8.GetBytes(json);
                using var hashAlgorithm = HashAlgorithm.Create("SHA-512");
                var hashBytes = hashAlgorithm.ComputeHash(bytes);
                var hashString = Convert.ToBase64String(hashBytes);
                return hashString;
            }

            private string Canonicalize(ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
            {
                string fieldsString = CanonicalizeFields(fields);
                string predecessorsString = CanonicalizePredecessors(predecessors);
                return $"{{\"fields\":{{{fieldsString}}},\"predecessors\":{{{predecessorsString}}}}}";
            }

            private string CanonicalizeFields(ImmutableList<Field> fields)
            {
                var serializedFields = fields
                    .OrderBy(field => field.Name)
                    .Select(field => $"\"{field.Name}\":{SerializeFieldValue(field.Value)}")
                    .ToArray();
                var result = String.Join(",", serializedFields);
                return result;
            }

            private string SerializeFieldValue(FieldValue value)
            {
                switch (value)
                {
                    case FieldValueString str:
                        return JsonSerializer.Serialize(str.StringValue);
                    default:
                        throw new NotImplementedException();
                }
            }

            private string CanonicalizePredecessors(ImmutableList<Predecessor> predecessors)
            {
                var serializedPredecessors = predecessors
                    .OrderBy(predecessor => predecessor.Role)
                    .Select(predecessor => $"\"{predecessor.Role}\":{SerializePredecessor(predecessor)}")
                    .ToArray();
                var result = String.Join(",", serializedPredecessors);
                return result;
            }

            private string SerializePredecessor(Predecessor predecessor)
            {
                switch (predecessor)
                {
                    case PredecessorSingle single:
                        return SerializeFactReference(single.Reference);
                    default:
                        throw new NotImplementedException();
                }
            }

            private string SerializeFactReference(FactReference reference)
            {
                string serializedType = JsonSerializer.Serialize(reference.Type);
                return $"{{\"hash\":\"{reference.Hash}\",\"type\":{serializedType}}}";
            }
        }

        public static TFact Deserialize<TFact>(ImmutableList<Fact> facts, FactReference reference)
        {
            throw new NotImplementedException();
        }
    }
}
