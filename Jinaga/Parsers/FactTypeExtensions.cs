using System;
using System.Linq;

namespace Jinaga.Parsers
{
    public static class FactTypeExtensions
    {
        public static string FactTypeName(this Type type)
        {
            return type.GetCustomAttributes(inherit: false)
                .OfType<FactTypeAttribute>()
                .Single()
                .Type;
        }
    }
}
