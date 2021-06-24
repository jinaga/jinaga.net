using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Jinaga.Definitions;

namespace Jinaga.Parsers
{
    public static class ValueParser
    {
        public static (string, SymbolValue) ParseValue(SymbolTable symbolTable, Expression expression)
        {
            if (expression is NewExpression newBody)
            {
                var names = newBody.Members
                    .Select(member => member.Name);
                var values = newBody.Arguments
                    .Select(arg => ParseValue(symbolTable, arg).Item2);
                var fields = names.Zip(values, (name, value) => KeyValuePair.Create(name, value))
                    .ToImmutableDictionary();
                return ("", new SymbolValueComposite(fields));
            }
            else if (expression is MemberExpression {
                Member: PropertyInfo propertyInfo
            } memberExpression)
            {
                var (tag, value) = ParseValue(symbolTable, memberExpression.Expression);
                if (value is SymbolValueComposite compositeValue)
                {
                    return (propertyInfo.Name, compositeValue.GetField(propertyInfo.Name));
                }
                else
                {
                    return (null, null);
                }
            }
            if (expression is ParameterExpression parameter)
            {
                return (parameter.Name, symbolTable.GetField(parameter.Name));
            }
            else if (expression is ConstantExpression)
            {
                return ("this", symbolTable.GetField("this"));
            }
            else
            {
                return (null, null);
            }
        }
    }
}
