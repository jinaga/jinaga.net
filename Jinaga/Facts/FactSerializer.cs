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
                    .Where(property => !IsField(property) && !IsCondition(property))
                    .Select(property => SerializePredecessor(property, runtimeFact))
                    .ToImmutableList();
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
        }
    }
}
