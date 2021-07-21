using System.Collections.Generic;
using System.Linq;

namespace Jinaga.Visualizers
{
    public static class Strings
    {
        public static string Indent(int depth)
        {
            return new string(' ', depth * 4);
        }

        public static string Join<T>(this IEnumerable<T> elements, string separator)
        {
            return string.Join(separator, elements.Select(e => e == null ? "null" : e.ToString()));
        }
    }
}
