using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Jinaga.Visualizers
{
    public class ExpressionVisualizer
    {
        public static string DumpExpression(Expression expression, int depth = 0)
        {
            Type expressionType = expression.GetType();
            string type = DumpType(expressionType);
            var parameters = expressionType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => $"{property.Name} = {DumpValue(property.GetValue(expression), depth + 1)}")
                .ToArray();
            var observation = $"new {type}\n{Indent(depth)}{{\n{Indent(depth + 1)}{string.Join(",\n" + Indent(depth + 1), parameters)}\n{Indent(depth)}}}";
            return observation;
        }

        private static string DumpType(Type type)
        {
            if (type.IsGenericType)
            {
                var arguments = type.GetGenericArguments()
                    .Select(DumpType)
                    .ToArray();
                string baseName = type.Name.Split("`")[0];
                return $"{baseName}<{string.Join(", ", arguments)}>";
            }
            else
            {
                return type.ToString();
            }
        }

        private static string DumpValue(object value, int depth)
        {
            if (value == null)
                return "null";
            Type type = value.GetType();
            if (typeof(Expression).IsAssignableFrom(type))
                return DumpExpression((Expression)value, depth);
            else if (type == typeof(string))
                return $"\"{value}\"";
            else if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                var values = ((IEnumerable)value)
                    .OfType<object>()
                    .Select(v => DumpValue(v, depth + 1))
                    .ToArray();
                return $"new []\n{Indent(depth)}{{\n{Indent(depth + 1)}{string.Join(",\n" + Indent(depth + 1), values)}\n{Indent(depth)}}}";
            }
            else
                return value.ToString();
        }

        private static string Indent(int depth)
        {
            return new string(' ', depth * 4);
        }
    }
}
