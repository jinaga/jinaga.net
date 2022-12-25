using Jinaga.Definitions;
using Jinaga.Generators;
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
        public static (SymbolValue symbolValue, string tag) ParseValue(SymbolTableOld symbolTable, SpecificationContext context, Expression expression)
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
                        if (propertyInfo.PropertyType.IsFactType() || propertyInfo.PropertyType.IsArrayOfFactType())
                        {
                            var predecessorType = propertyInfo.PropertyType.FactTypeName();
                            var setDefinition = setValue.SetDefinition.AppendChain(role, predecessorType, propertyInfo.PropertyType);
                            return (new SymbolValueSetDefinition(setDefinition), tag);
                        }
                        else
                        {
                            var factRuntimeType = memberExpression.Expression.Type;
                            return (new SymbolValueField(setValue.SetDefinition, factRuntimeType, propertyInfo.Name), tag);
                        }
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
            else if (expression is MethodCallExpression observableCallExpression &&
                observableCallExpression.Method.DeclaringType == typeof(FactRepositoryOld) &&
                observableCallExpression.Method.Name == nameof(FactRepositoryOld.Observable) &&
                observableCallExpression.Arguments.Count == 2 &&
                typeof(Specification).IsAssignableFrom(observableCallExpression.Arguments[1].Type))
            {
                var start = ParseValue(symbolTable, context, observableCallExpression.Arguments[0]).symbolValue;
                if (start is SymbolValueSetDefinition startValue)
                {
                    var startSetDefinition = startValue.SetDefinition;
                    var specification = ParseSpecification(observableCallExpression.Arguments[1]);
                    var arguments = ImmutableList<Pipelines.Label>.Empty.Add(startSetDefinition.Label);
                    specification = specification.Apply(arguments);
                    return (new SymbolValueCollection(specification.Matches, specification.Projection), "");
                }
                else
                {
                    throw new NotImplementedException($"ParseValue: {ExpressionVisualizer.DumpExpression(expression)}");
                }
            }
            else if (expression is MethodCallExpression observableIQueryableCallExpression &&
                observableIQueryableCallExpression.Method.DeclaringType == typeof(FactRepositoryOld) &&
                observableIQueryableCallExpression.Method.Name == nameof(FactRepositoryOld.Observable) &&
                observableIQueryableCallExpression.Arguments.Count == 1 &&
                typeof(IQueryable).IsAssignableFrom(observableIQueryableCallExpression.Arguments[0].Type))
            {
                var result = SpecificationParser.ParseSpecification(symbolTable, context, observableIQueryableCallExpression.Arguments[0]);
                var matches = SpecificationGenerator.CreateMatches(context, result);
                var projection = SpecificationGenerator.CreateProjection(result.SymbolValue);
                return (new SymbolValueCollection(matches, projection), "");
            }
            else if (typeof(IQueryable).IsAssignableFrom(expression.Type))
            {
                var result = SpecificationParser.ParseSpecification(symbolTable, context, expression);
                var matches = SpecificationGenerator.CreateMatches(context, result);
                var projection = SpecificationGenerator.CreateProjection(result.SymbolValue);
                return (new SymbolValueCollection(matches, projection), "");
            }
            else
            {
                throw new NotImplementedException($"ParseValue: {ExpressionVisualizer.DumpExpression(expression)}");
            }
        }

        private static KeyValuePair<string, SymbolValue> ParseMemberBinding(SymbolTableOld symbolTable, SpecificationContext context, MemberBinding binding)
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
