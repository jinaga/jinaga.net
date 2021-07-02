using System.Reflection;
using System;
using System.Collections.Immutable;
using Jinaga.Parsers;
using System.Linq;

namespace Jinaga.Facts
{
    public class FactSerializer
    {
        public static Fact Serialize(object runtimeFact)
        {
            Type runtimeType = runtimeFact.GetType();
            string type = runtimeType.FactTypeName();
            var fields = runtimeType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(runtimeField => SerializeField(runtimeField, runtimeFact))
                .ToImmutableList();
            return new Fact(type, fields);
        }

        private static Field SerializeField(PropertyInfo runtimeField, object runtimeFact)
        {
            var value = runtimeField.PropertyType == typeof(string)
                ? new FieldValueString((string)runtimeField.GetValue(runtimeFact))
                : throw new ArgumentException($"Unsupported field type {runtimeField.PropertyType.Name} in {runtimeField.DeclaringType.Name}.{runtimeField.Name}");
            return new Field(runtimeField.Name, value);
        }
    }
}
