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
        public static SymbolValue ParseValue(SymbolTable symbolTable, Expression expression)
        {
            if (expression is NewExpression newBody)
            {
                var names = newBody.Members
                    .Select(member => member.Name);
                var values = newBody.Arguments
                    .Select(arg => ParseValue(symbolTable, arg));
                var fields = names.Zip(values, (name, value) => KeyValuePair.Create(name, value))
                    .ToImmutableDictionary();
                return new SymbolValueComposite(fields);
            }
            else if (expression is MemberExpression {
                Member: PropertyInfo propertyInfo
            } memberExpression)
            {
                switch (ParseValue(symbolTable, memberExpression.Expression))
                {
                    case SymbolValueComposite compositeValue:
                        return compositeValue.GetField(propertyInfo.Name);
                    case SymbolValueSetDefinition setValue:
                        var role = propertyInfo.Name;
                        var predecessorType = propertyInfo.PropertyType.FactTypeName();
                        var setDefinition = setValue.SetDefinition.AppendChain(role, predecessorType);
                        return new SymbolValueSetDefinition(setDefinition);
                    default:
                        throw new NotImplementedException();
                }
            }
            else if (expression is ParameterExpression parameter)
            {
                return symbolTable.GetField(parameter.Name);
            }
            else if (expression is ConstantExpression)
            {
                return symbolTable.GetField("this");
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
