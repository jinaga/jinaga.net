using System;
using System.Linq;

namespace Jinaga.Serialization
{
    static class Interrogate
    {
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

        public static bool IsFactType(Type type)
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
    }
}
