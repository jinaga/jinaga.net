using Jinaga.Definitions;
using Jinaga.Projections;
using Jinaga.Repository;
using Jinaga.Visualizers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Jinaga.Parsers
{
    public static class ValueParser
    {
        public static (SymbolValue symbolValue, string tag) ParseValue(SymbolTable symbolTable, SpecificationContext context, Expression expression)
        {
            if (expression is NewExpression newBody)
            {
                var names = newBody.Members != null
                    ? newBody.Members.Select(member => member.Name)
                    : newBody.Constructor.GetParameters().Select(parameter => parameter.Name);
                var values = newBody.Arguments
                    .Select(arg => ParseValue(symbolTable, context, arg).symbolValue);
                var fields = names.Zip(values, (name, value) => KeyValuePair.Create(name, value))
                    .ToImmutableDictionary();
                return (new SymbolValueComposite(fields), "");
            }
            else if (expression is MemberInitExpression memberInit)
            {
                var fields = memberInit.Bindings
                    .Select(binding => ParseMemberBinding(symbolTable, context, binding))
                    .ToImmutableDictionary();
                return (new SymbolValueComposite(fields), "");
            }
            else if (expression is MemberExpression {
                Member: PropertyInfo propertyInfo
            } memberExpression)
            {
                switch (ParseValue(symbolTable, context, memberExpression.Expression))
                {
                    case (SymbolValueComposite compositeValue, _):
                        return (compositeValue.GetField(propertyInfo.Name), propertyInfo.Name);
                    case (SymbolValueSetDefinition setValue, string tag):
                        var role = propertyInfo.Name;
                        var predecessorType = propertyInfo.PropertyType.FactTypeName();
                        var setDefinition = setValue.SetDefinition.AppendChain(role, predecessorType, propertyInfo.PropertyType);
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
            })
            {
                var lambdaExpression = Expression.Lambda<Func<object>>(expression);
                object value = lambdaExpression.Compile().Invoke();
                var variable = context.GetVariable(value);
                var setDefinition = new SetDefinitionInitial(variable.Label, variable.Type);
                return (new SymbolValueSetDefinition(setDefinition), variable.Label.Name);
            }
            else if (expression is MethodCallExpression allCallExpression &&
                allCallExpression.Method.DeclaringType == typeof(FactRepository) &&
                allCallExpression.Method.Name == nameof(FactRepository.All))
            {
                var start = ParseValue(symbolTable, context, allCallExpression.Arguments[0]).symbolValue;
                if (start is SymbolValueSetDefinition startValue)
                {
                    var startSetDefinition = startValue.SetDefinition;
                    var specification = ParseSpecification(allCallExpression.Arguments[1]);
                    var parameterLabel = specification.Pipeline.Starts.First();
                    var argument = startSetDefinition.Label;
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

        private static KeyValuePair<string, SymbolValue> ParseMemberBinding(SymbolTable symbolTable, SpecificationContext context, MemberBinding binding)
        {
            if (binding is MemberAssignment assignment)
            {
                var name = assignment.Member.Name;
                var value = ParseValue(symbolTable, context, assignment.Expression).symbolValue;
                return KeyValuePair.Create(name, value);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static Specification ParseSpecification(Expression expression)
        {
            var lambdaExpression = Expression.Lambda<Func<object>>(expression);
            return (Specification)lambdaExpression.Compile().Invoke();
        }
    }
}
