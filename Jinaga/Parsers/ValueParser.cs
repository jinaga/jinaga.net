using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Jinaga.Definitions;
using Jinaga.Visualizers;

namespace Jinaga.Parsers
{
    public static class ValueParser
    {
        public static (SymbolValue symbolValue, string tag) ParseValue(SymbolTable symbolTable, Expression expression)
        {
            if (expression is NewExpression newBody)
            {
                var names = newBody.Members
                    .Select(member => member.Name);
                var values = newBody.Arguments
                    .Select(arg => ParseValue(symbolTable, arg).symbolValue);
                var fields = names.Zip(values, (name, value) => KeyValuePair.Create(name, value))
                    .ToImmutableDictionary();
                return (new SymbolValueComposite(fields), "");
            }
            else if (expression is MemberExpression {
                Member: PropertyInfo propertyInfo
            } memberExpression)
            {
                switch (ParseValue(symbolTable, memberExpression.Expression))
                {
                    case (SymbolValueComposite compositeValue, _):
                        return (compositeValue.GetField(propertyInfo.Name), propertyInfo.Name);
                    case (SymbolValueSetDefinition setValue, string tag):
                        var role = propertyInfo.Name;
                        var predecessorType = propertyInfo.PropertyType.FactTypeName();
                        var setDefinition = setValue.SetDefinition.AppendChain(role, predecessorType);
                        return (new SymbolValueSetDefinition(setDefinition), tag);
                    default:
                        throw new NotImplementedException();
                }
            }
            else if (expression is ParameterExpression parameter)
            {
                return (symbolTable.GetField(parameter.Name), parameter.Name);
            }
            else if (expression is ConstantExpression)
            {
                return (symbolTable.GetField("this"), "this");
            }
            else if (expression is MemberExpression {
                Expression: ConstantExpression {}
            } constantMemberExpression)
            {
                var type = constantMemberExpression.Type.FactTypeName();
                var name = constantMemberExpression.Member.Name;
                var setDefinition = new SetDefinitionInitial(name, type);
                return (new SymbolValueSetDefinition(setDefinition), name);
            }
            else
            {
                throw new NotImplementedException($"ParseValue: {ExpressionVisualizer.DumpExpression(expression)}");
            }
        }
    }
}
