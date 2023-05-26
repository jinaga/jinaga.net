﻿using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Specifications;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Jinaga.Repository
{
    class SpecificationProcessor
    {
        private ImmutableList<Label> labels = ImmutableList<Label>.Empty;
        private ImmutableList<Label> givenLabels = ImmutableList<Label>.Empty;

        public static (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Queryable<TProjection>(LambdaExpression specExpression)
        {
            var processor = new SpecificationProcessor();
            var symbolTable = processor.Given(specExpression.Parameters
                .Take(specExpression.Parameters.Count - 1));
            var result = processor.ProcessSource(specExpression.Body, symbolTable);
            processor.ValidateMatches(result.Matches);
            return (processor.givenLabels, result.Matches, result.Projection);
        }

        public static (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Scalar<TProjection>(LambdaExpression specExpression)
        {
            var processor = new SpecificationProcessor();
            var symbolTable = processor.Given(specExpression.Parameters);
            SourceContext result = processor.ProcessShorthand(specExpression.Body, symbolTable);
            processor.ValidateMatches(result.Matches);
            return (processor.givenLabels, result.Matches, result.Projection);
        }

        private SymbolTable Given(IEnumerable<ParameterExpression> parameters)
        {
            givenLabels = parameters
                .Select(parameter => NewLabel(parameter.Name, parameter.Type.FactTypeName()))
                .ToImmutableList();
            var symbolTable = parameters.Aggregate(
                SymbolTable.Empty,
                (table, parameter) => table.Set(parameter.Name, Value.Simple(parameter.Name)));
            return symbolTable;
        }

        private Label NewLabel(string recommendedName, string factType)
        {
            var source = new Label(recommendedName, factType);
            labels = labels.Add(source);
            return source;
        }

        private SourceContext ProcessShorthand(Expression expression, SymbolTable symbolTable)
        {
            var reference = ProcessReference(expression, symbolTable);
            if (reference.Roles.Any())
            {
                // Label the tail of the predecessor chain.
                var lastRole = reference.Roles.Last();
                var unknown = new Label(lastRole.Name, lastRole.TargetType);
                var self = ReferenceContext.From(unknown);
                return LinqProcessor.Where(
                    LinqProcessor.FactsOfType(unknown),
                    LinqProcessor.Compare(
                        reference, self
                    )
                );
            }
            throw new SpecificationException($"A shorthand specification must select predecessors: {expression}");
        }

        private Projection ProcessProjection(Expression expression, SymbolTable symbolTable)
        {
            if (expression is ParameterExpression parameterExpression)
            {
                return symbolTable.Get(parameterExpression.Name).Projection;
            }
            else if (expression is NewExpression newExpression)
            {
                var names = newExpression.Members != null
                    ? newExpression.Members.Select(member => member.Name)
                    : newExpression.Constructor.GetParameters().Select(parameter => parameter.Name);
                var values = newExpression.Arguments
                    .Select(arg => ProcessProjection(arg, symbolTable));
                var fields = names.Zip(values, (name, value) => KeyValuePair.Create(name, value))
                    .ToImmutableDictionary();
                var compoundProjection = new CompoundProjection(fields);
                return compoundProjection;
            }
            else if (expression is MemberInitExpression memberInit)
            {
                var fields = memberInit.Bindings
                    .Select(binding => ProcessProjectionMember(binding, symbolTable))
                    .ToImmutableDictionary();
                var compoundProjection = new CompoundProjection(fields);
                return compoundProjection;
            }
            else if (expression is MemberExpression memberExpression)
            {
                var head = ProcessProjection(memberExpression.Expression, symbolTable);
                if (head is CompoundProjection compoundProjection)
                {
                    return compoundProjection.GetProjection(memberExpression.Member.Name);
                }
                else if (head is SimpleProjection simpleProjection)
                {
                    if (!memberExpression.Type.IsFactType())
                    {
                        return new FieldProjection(simpleProjection.Tag, memberExpression.Expression.Type, memberExpression.Member.Name);
                    }
                    else
                    {
                        throw new SpecificationException($"Cannot select {memberExpression.Member.Name} directly. Give the fact a label first.");
                    }
                }
            }
            else if (expression is MethodCallExpression methodCallExpression)
            {
                if (methodCallExpression.Method.DeclaringType == typeof(FactRepository) &&
                    methodCallExpression.Method.Name == nameof(FactRepository.Observable))
                {
                    if (methodCallExpression.Arguments.Count == 2 &&
                        typeof(Specification).IsAssignableFrom(methodCallExpression.Arguments[1].Type))
                    {
                        var start = ProcessProjection(methodCallExpression.Arguments[0], symbolTable);
                        var label = LabelOfProjection(start);
                        var lambdaExpression = Expression.Lambda<Func<object>>(methodCallExpression.Arguments[1]);
                        var specification = (Specification)lambdaExpression.Compile().Invoke();
                        var arguments = ImmutableList.Create(label);
                        specification = specification.Apply(arguments);
                        var collectionProjection = new CollectionProjection(specification.Matches, specification.Projection);
                        return collectionProjection;
                    }
                    else if (methodCallExpression.Arguments.Count == 1 &&
                        typeof(IQueryable).IsAssignableFrom(methodCallExpression.Arguments[0].Type))
                    {
                        var value = ProcessSource(methodCallExpression.Arguments[0], symbolTable);
                        var collectionProjection = new CollectionProjection(value.Matches, value.Projection);
                        return collectionProjection;
                    }
                }
                else if (expression.Type.IsGenericType && expression.Type.GetGenericTypeDefinition() == typeof(IQueryable<>))
                {
                    var value = ProcessSource(expression, symbolTable);
                    var collectionProjection = new CollectionProjection(value.Matches, value.Projection);
                    return collectionProjection;
                }
            }
            throw new SpecificationException($"Unsupported type of projection {expression}.");
        }

        private SourceContext ProcessSource(Expression expression, SymbolTable symbolTable, string recommendedLabel = "unknown")
        {
            if (expression is MethodCallExpression methodCallExpression)
            {
                if (methodCallExpression.Method.DeclaringType == typeof(Queryable))
                {
                    if (methodCallExpression.Method.Name == nameof(System.Linq.Queryable.Where) &&
                        methodCallExpression.Arguments.Count == 2)
                    {
                        var lambda = GetLambda(methodCallExpression.Arguments[1]);
                        var parameterName = lambda.Parameters[0].Name;
                        var source = ProcessSource(methodCallExpression.Arguments[0], symbolTable, parameterName);
                        var childSymbolTable = symbolTable.Set(parameterName, Value.From(source.Projection));
                        var predicate = ProcessPredicate(lambda.Body, childSymbolTable);
                        return LinqProcessor.Where(source, predicate);
                    }
                    else if (methodCallExpression.Method.Name == nameof(System.Linq.Queryable.Select) &&
                        methodCallExpression.Arguments.Count == 2)
                    {
                        var selector = GetLambda(methodCallExpression.Arguments[1]);
                        var parameterName = selector.Parameters[0].Name;
                        var source = ProcessSource(methodCallExpression.Arguments[0], symbolTable, parameterName);
                        var childSymbolTable = symbolTable.Set(parameterName, Value.From(source.Projection));
                        var projection = ProcessProjection(selector.Body, childSymbolTable);
                        return LinqProcessor.Select(source, projection);
                    }
                    else if (methodCallExpression.Method.Name == nameof(System.Linq.Queryable.SelectMany) &&
                        methodCallExpression.Arguments.Count == 3)
                    {
                        var collectionSelector = GetLambda(methodCallExpression.Arguments[1]);
                        var collectionSelectorParameterName = collectionSelector.Parameters[0].Name;
                        var resultSelector = GetLambda(methodCallExpression.Arguments[2]);
                        var resultSelectorParameterName = resultSelector.Parameters[1].Name;

                        var source = ProcessSource(methodCallExpression.Arguments[0], symbolTable, collectionSelectorParameterName);

                        var collectionSelectorSymbolTable = symbolTable.Set(collectionSelectorParameterName, Value.From(source.Projection));
                        var selector = ProcessSource(collectionSelector.Body, collectionSelectorSymbolTable, resultSelectorParameterName);

                        var resultSelectorSymbolTable = symbolTable
                            .Set(resultSelector.Parameters[0].Name, Value.From(source.Projection))
                            .Set(resultSelector.Parameters[1].Name, Value.From(selector.Projection));
                        var projection = ProcessProjection(resultSelector.Body, resultSelectorSymbolTable);

                        return LinqProcessor.Select(
                            LinqProcessor.SelectMany(source, selector),
                            projection);
                    }
                }
                else if (methodCallExpression.Method.DeclaringType == typeof(FactRepository))
                {
                    if (methodCallExpression.Method.Name == nameof(FactRepository.OfType) &&
                        methodCallExpression.Arguments.Count == 0)
                    {
                        string type = methodCallExpression.Method.GetGenericArguments()[0].FactTypeName();
                        var label = new Label(recommendedLabel, type);
                        return LinqProcessor.FactsOfType(label);
                    }
                    else if (methodCallExpression.Method.Name == nameof(FactRepository.OfType) &&
                        methodCallExpression.Arguments.Count == 1)
                    {
                        // Get the recommended label from the predicate.
                        var lambda = GetLambda(methodCallExpression.Arguments[0]);
                        var parameterName = lambda.Parameters[0].Name;

                        // Produce the source of the match.
                        var genericArgument = methodCallExpression.Method.GetGenericArguments()[0];
                        var source = LinqProcessor.FactsOfType(new Label(parameterName, genericArgument.FactTypeName()));

                        // Process the predicate.
                        var childSymbolTable = symbolTable.Set(parameterName, Value.From(source.Projection));
                        var predicate = ProcessPredicate(lambda.Body, childSymbolTable);
                        return LinqProcessor.Where(source, predicate);
                    }
                }
            }
            throw new SpecificationException($"Unsupported type of specification {expression}.");
        }

        private PredicateContext ProcessPredicate(Expression body, SymbolTable symbolTable)
        {
            if (body is BinaryExpression { NodeType: ExpressionType.Equal } binary)
            {
                var left = ProcessReference(binary.Left, symbolTable);
                var right = ProcessReference(binary.Right, symbolTable);
                return LinqProcessor.Compare(left, right);
            }
            if (body is UnaryExpression { NodeType: ExpressionType.Not, Operand: Expression operand })
            {
                return LinqProcessor.Not(ProcessPredicate(operand, symbolTable));
            }
            if (body is MethodCallExpression methodCallExpression)
            {
                if (methodCallExpression.Method.DeclaringType == typeof(Queryable))
                {
                    if (methodCallExpression.Method.Name == nameof(System.Linq.Queryable.Any) &&
                        methodCallExpression.Arguments.Count == 1)
                    {
                        var source = ProcessSource(methodCallExpression.Arguments[0], symbolTable);
                        return LinqProcessor.Any(source);
                    }
                }
                else if (methodCallExpression.Method.DeclaringType == typeof(Enumerable) &&
                    methodCallExpression.Method.Name == nameof(System.Linq.Enumerable.Contains) &&
                    methodCallExpression.Arguments.Count == 2)
                {
                    var left = ProcessReference(methodCallExpression.Arguments[0], symbolTable);
                    var right = ProcessReference(methodCallExpression.Arguments[1], symbolTable);
                    return LinqProcessor.Compare(left, right);
                }
            }
            else if (body is UnaryExpression
            {
                Operand: MemberExpression
                {
                    Member: PropertyInfo propertyInfo
                } member,
                NodeType: ExpressionType.Convert
            } unaryExpression)
            {
                if (propertyInfo.PropertyType == typeof(Condition) &&
                    unaryExpression.Type == typeof(Boolean))
                {
                    object target = InstanceOfFact(propertyInfo.DeclaringType);
                    var condition = (Condition)propertyInfo.GetGetMethod().Invoke(target, new object[0]);
                    var projection = ProcessProjection(member.Expression, symbolTable);
                    var childSymbolTable = symbolTable.Set("this", Value.From(projection));
                    return ProcessPredicate(condition.Body.Body, childSymbolTable);
                }
            }
            throw new SpecificationException($"Unsupported predicate type {body}.");
        }

        private ReferenceContext ProcessReference(Expression expression, SymbolTable symbolTable)
        {
            if (expression is ParameterExpression parameterExpression)
            {
                var projection = symbolTable.Get(parameterExpression.Name).Projection;
                if (projection is SimpleProjection simpleProjection)
                {
                    var type = parameterExpression.Type.FactTypeName();
                    return ReferenceContext.From(new Label(simpleProjection.Tag, type));
                }
            }
            else if (expression is ConstantExpression)
            {
                var projection = symbolTable.Get("this").Projection;
                if (projection is SimpleProjection simpleProjection)
                {
                    var type = expression.Type.FactTypeName();
                    return ReferenceContext.From(new Label(simpleProjection.Tag, type));
                }
            }
            else if (expression is MemberExpression memberExpression)
            {
                if (memberExpression.Expression.Type.IsFactType())
                {
                    var head = ProcessReference(memberExpression.Expression, symbolTable);
                    return head.Push(new Role(memberExpression.Member.Name, memberExpression.Type.FactTypeName()));
                }
                else
                {
                    var head = ProcessProjection(memberExpression.Expression, symbolTable);
                    if (head is CompoundProjection compoundProjection)
                    {
                        var member = compoundProjection.GetProjection(memberExpression.Member.Name);
                        if (member is SimpleProjection simpleProjection)
                        {
                            var type = memberExpression.Type.FactTypeName();
                            return ReferenceContext.From(new Label(simpleProjection.Tag, type));
                        }
                    }
                }
            }
            throw new SpecificationException($"Unsuported reference {expression}."); ;
        }

        private void ValidateMatches(ImmutableList<Match> matches)
        {
            // Look for matches with no path conditions.
            Match? priorMatch = null;
            foreach (var match in matches)
            {
                if (!match.Conditions.OfType<PathCondition>().Any())
                {
                    var unknown = match.Unknown.Name;
                    var prior = priorMatch != null
                        ? $"prior variable \"{priorMatch.Unknown.Name}\""
                        : $"parameter \"{givenLabels.First().Name}\"";
                    throw new SpecificationException($"The variable \"{unknown}\" should be joined to the {prior}.");
                }
                priorMatch = match;
            }
        }

        private KeyValuePair<string, Projection> ProcessProjectionMember(MemberBinding binding, SymbolTable symbolTable)
        {
            if (binding is MemberAssignment assignment)
            {
                var name = assignment.Member.Name;
                var value = ProcessProjection(assignment.Expression, symbolTable);
                return KeyValuePair.Create(name, value);
            }
            else
            {
                throw new SpecificationException($"Unsupported projection member {binding}.");
            }
        }

        private static LambdaExpression GetLambda(Expression argument)
        {
            if (argument is UnaryExpression unaryExpression &&
                unaryExpression.Operand is LambdaExpression lambdaExpression)
            {
                return lambdaExpression;
            }
            else
            {
                throw new ArgumentException($"Expected a unary lambda expression for {argument}.");
            }
        }

        private static string LabelOfProjection(Projection projection)
        {
            // Expect the projection to be a simple one.
            if (projection is SimpleProjection simpleProjection)
            {
                return simpleProjection.Tag;
            }
            else
            {
                throw new ArgumentException($"Expected a simple projection, but got {projection.GetType().Name}.");
            }
        }

        public static object InstanceOfFact(Type factType)
        {
            var constructor = factType.GetConstructors().First();
            var parameters = constructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Select(type => type.IsValueType ? Activator.CreateInstance(type) : InstanceOfFact(type))
                .ToArray();
            return Activator.CreateInstance(factType, parameters);
        }
    }
}
