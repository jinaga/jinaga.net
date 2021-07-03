using System.Reflection;
using System;
using System.Collections.Immutable;
using Jinaga.Parsers;
using System.Linq;

namespace Jinaga.Facts
{
    public class FactSerializer
    {
        public static ImmutableList<Fact> Serialize(object runtimeFact)
        {
            var collector = new Collector();
            FactReference reference = collector.Serialize(runtimeFact);
            return collector.Facts;
        }

        private class Collector
        {
            public ImmutableList<Fact> Facts { get; set; } = ImmutableList<Fact>.Empty;

            public FactReference Serialize(object runtimeFact)
            {
                Type runtimeType = runtimeFact.GetType();
                string type = runtimeType.FactTypeName();
                var reference = new FactReference(type, "");
                var properties = runtimeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var fields = properties
                    .Where(property => IsField(property))
                    .Select(property => SerializeField(property, runtimeFact))
                    .ToImmutableList();
                var predecessors = properties
                    .Where(property => !IsField(property))
                    .Select(property => SerializePredecessor(property, runtimeFact))
                    .ToImmutableList();
                Facts = Facts.Add(new Fact(reference, fields, predecessors));
                return reference;
            }

            private static bool IsField(PropertyInfo runtimeField)
            {
                return
                    runtimeField.PropertyType == typeof(string) ||
                    runtimeField.PropertyType == typeof(DateTime);
            }

            private static Field SerializeField(PropertyInfo property, object runtimeFact)
            {
                var value = property.PropertyType == typeof(string)
                    ? new FieldValueString((string)property.GetValue(runtimeFact))
                    : property.PropertyType == typeof(DateTime)
                    ? new FieldValueString(ToIso8601String((DateTime)property.GetValue(runtimeFact)))
                    : throw new ArgumentException($"Unsupported field type {property.PropertyType.Name} in {property.DeclaringType.Name}.{property.Name}");
                return new Field(property.Name, value);
            }

            private Predecessor SerializePredecessor(PropertyInfo property, object runtimeFact)
            {
                string role = property.Name;
                var reference = Serialize(property.GetValue(runtimeFact));
                return new PredecessorSingle(role, reference);
            }

            private static string ToIso8601String(DateTime dateTime)
            {
                var utcDateTime = dateTime.Kind == DateTimeKind.Utc
                    ? dateTime
                    : dateTime.ToUniversalTime();
                return utcDateTime.ToString("yyyy-MM-ddThh:mm:ss.fffZ");
            }
        }
    }
}
