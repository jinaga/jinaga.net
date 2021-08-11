using System;
using System.Linq;

namespace Jinaga.Parsers
{
    public static class FactTypeExtensions
    {
        public static bool IsFactType(this Type type)
        {
            return !type.IsArray && type.GetCustomAttributes(inherit: false)
                .OfType<FactTypeAttribute>()
                .Any();
        }
        
        public static bool IsArrayOfFactType(this Type type)
        {
            return type.IsArray && type.GetElementType().IsFactType();
        }

        public static string FactTypeName(this Type type)
        {
            if (type.IsArray)
            {
                return FactTypeName(type.GetElementType());
            }
            else
            {
                var attributes = type.GetCustomAttributes(inherit: false)
                    .OfType<FactTypeAttribute>();
                if (!attributes.Any())
                {
                    throw new ArgumentException($"Type {type.FullName} is not a fact");
                }
                return attributes
                    .Single()
                    .Type;
            }
        }
    }
}
