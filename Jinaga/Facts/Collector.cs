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
    class Collector
    {
        public FactGraph Graph { get; private set; } = new FactGraph();

        public int FactVisitsCount { get; private set; } = 0;
        public SerializerCache SerializerCache { get; private set; }

        public ImmutableHashSet<object> visiting =
            ImmutableHashSet<object>.Empty;
        public ImmutableDictionary<object, FactReference> referenceByObject =
            ImmutableDictionary<object, FactReference>.Empty;

        public Collector() : this(new SerializerCache())
        {
        }

        public Collector(SerializerCache serializerCache)
        {
            this.SerializerCache = serializerCache;
        }

        public FactReference Serialize(object runtimeFact)
        {
            if (!referenceByObject.TryGetValue(runtimeFact, out var reference))
            {
                if (visiting.Contains(runtimeFact))
                {
                    throw new ArgumentException("Jinaga cannot serialize a fact containing a cycle");
                }
                visiting = visiting.Add(runtimeFact);
                FactVisitsCount++;

                var runtimeType = runtimeFact.GetType();
                Func<object, Fact> serializer = GetSerializer(runtimeType);
                var fact = serializer(runtimeFact);
                reference = fact.Reference;

                Graph = Graph.Add(fact);
                referenceByObject = referenceByObject.Add(runtimeFact, reference);
            }
            return reference;
        }

        private Func<object, Fact> GetSerializer(Type runtimeType)
        {
            
            return runtimeFact =>
            {
                var properties = runtimeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var fields = properties
                    .Where(property => IsField(property.PropertyType))
                    .Select(property => SerializeField(property, runtimeFact))
                    .ToImmutableList();
                var predecessors = properties
                    .Where(property => IsPredecessor(property.PropertyType))
                    .Select(property => SerializePredecessor(property, runtimeFact))
                    .ToImmutableList();

                var reference = new FactReference(runtimeType.FactTypeName(), ComputeHash(fields, predecessors));
                return new Fact(reference, fields, predecessors);
            };
        }

        public static bool IsField(Type type)
        {
            return
                type == typeof(string) ||
                type == typeof(DateTime) ||
                type == typeof(int) ||
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(bool);
        }

        public static bool IsPredecessor(Type type)
        {
            return
                IsFactType(type) ||
                IsArrayOfFactType(type);
        }

        private static bool IsFactType(Type type)
        {
            return type
                .GetCustomAttributes(inherit: false)
                .OfType<FactTypeAttribute>()
                .Any();
        }

        private static bool IsArrayOfFactType(Type type)
        {
            return
                type.IsArray &&
                IsFactType(type.GetElementType());
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
            if (!property.PropertyType.IsArray)
            {
                var reference = Serialize(property.GetValue(runtimeFact));
                return new PredecessorSingle(role, reference);
            }
            else
            {
                var array = (object[])property.GetValue(runtimeFact);
                var references = array
                    .Select(obj => Serialize(obj))
                    .ToImmutableList();
                return new PredecessorMultiple(role, references);
            }
        }

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
