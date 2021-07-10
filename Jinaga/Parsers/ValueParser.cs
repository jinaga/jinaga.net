using System;
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
        public static (string, SymbolValue)? ParseValue(SymbolTable symbolTable, Expression expression)
        {
            if (expression is NewExpression newBody)
            {
                var names = newBody.Members
                    .Select(member => member.Name);
                var values = newBody.Arguments
                    .Select(arg =>
                    {
                        switch (ParseValue(symbolTable, arg))
                        {
                            case (string _, SymbolValue sv):
                                return sv;
                            default:
                                throw new NotImplementedException();
                        }
                    });
                var fields = names.Zip(values, (name, value) => KeyValuePair.Create(name, value))
                    .ToImmutableDictionary();
                return ("", new SymbolValueComposite(fields));
            }
            else if (expression is MemberExpression {
                Member: PropertyInfo propertyInfo
            } memberExpression)
            {
                switch (ParseValue(symbolTable, memberExpression.Expression))
                {
                    case (string tag, SymbolValueComposite compositeValue):
                        return (propertyInfo.Name, compositeValue.GetField(propertyInfo.Name));
                    default:
                        return null;
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
                return null;
            }
        }
    }
}
