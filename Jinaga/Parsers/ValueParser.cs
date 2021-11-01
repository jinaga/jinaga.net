using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Jinaga.Definitions;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Repository;
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
            else if (expression is MemberInitExpression memberInit)
            {
                var fields = memberInit.Bindings
                    .Select(binding => ParseMemberBinding(symbolTable, binding))
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
            else if (expression is MethodCallExpression allCallExpression &&
                allCallExpression.Method.DeclaringType == typeof(FactRepository) &&
                allCallExpression.Method.Name == nameof(FactRepository.All))
            {
                var start = ParseValue(symbolTable, allCallExpression.Arguments[0]).symbolValue;
                if (start is SymbolValueSetDefinition startValue)
                {
                    var startSetDefinition = startValue.SetDefinition;
                    var specification = ParseSpecification(symbolTable, allCallExpression.Arguments[1]);
                    var parameterLabel = specification.Pipeline.Starts.First();
                    var argument = new Label(startSetDefinition.Tag, startSetDefinition.FactType);
                    var pipeline = specification.Pipeline.Apply(parameterLabel, argument);
                    var projection = specification.Projection.Apply(parameterLabel, argument);
                    var specificationObj = new Specification(pipeline, projection);
                    return (new SymbolValueCollection(startSetDefinition, specificationObj), "");
                }
                else
                {
                    throw new NotImplementedException($"ParseValue: {ExpressionVisualizer.DumpExpression(expression)}");
                }
            }
            else
            {
                throw new NotImplementedException($"ParseValue: {ExpressionVisualizer.DumpExpression(expression)}");
            }
        }

        private static KeyValuePair<string, SymbolValue> ParseMemberBinding(SymbolTable symbolTable, MemberBinding binding)
        {
            if (binding is MemberAssignment assignment)
            {
                var name = assignment.Member.Name;
                var value = ParseValue(symbolTable, assignment.Expression).symbolValue;
                return KeyValuePair.Create(name, value);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static Specification ParseSpecification(SymbolTable symbolTable, Expression expression)
        {
            if (expression is MemberExpression {
                Expression: null,
                Member: FieldInfo staticField
            })
            {
                return (Specification)staticField.GetValue(null);
            }
            else if (expression is MemberExpression {
                Expression: ConstantExpression constantExpression,
                Member: FieldInfo field
            })
            {
                return (Specification)field.GetValue(constantExpression.Value);
            }
            else
            {
                throw new NotImplementedException($"ParseSpecification: {ExpressionVisualizer.DumpExpression(expression)}");
            }
        }
    }
}
