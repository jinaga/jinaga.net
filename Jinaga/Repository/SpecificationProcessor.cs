﻿using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Specifications;
using Jinaga.Visualizers;
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
            var result = processor.ProcessQueryable(specExpression.Body, symbolTable);
            processor.ValidateMatches(result.Matches);
            return (processor.givenLabels, result.Matches, result.Projection);
        }

        public static (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Scalar<TProjection>(LambdaExpression specExpression)
        {
            var processor = new SpecificationProcessor();
            var symbolTable = processor.Given(specExpression.Parameters);
            var result = processor.ProcessShorthand(ImmutableList<Match>.Empty, specExpression.Body, symbolTable);
            return processor.ProcessResult<TProjection>(result);
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

        private Value ProcessShorthand(ImmutableList<Match> matches, Expression expression, SymbolTable symbolTable, string recommendedLabel = "unknown")
        {
            if (expression is MemberExpression memberExpression)
            {
                // Trace predecessors up from the source label.
                var (projection, roles) = ProcessJoinExpression(memberExpression, symbolTable);

                if (roles.Any())
                {
                    string label = LabelOfProjection(projection);

                    // Label the tail of the predecessor chain.
                    var lastRole = roles.Last();
                    var value = AllocateLabel(matches, lastRole.Name, lastRole.TargetType);

                    // Add the path condition to the match.
                    var match = value.Matches.Last();
                    var pathCondition = new PathCondition(ImmutableList<Role>.Empty, label, roles);
                    var newMatch = new Match(match.Unknown, match.Conditions.Add(pathCondition));
                    var newMatches = value.Matches.Replace(match, newMatch);
                    return new Value(newMatches, value.Projection);
                }
                else
                {
                    return new Value(matches, projection);
                }
            }
            else
            {
                throw new SpecificationException($"A shorthand specification must select predecessors: {expression}");
            }
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
                        var value = ProcessQueryable(methodCallExpression.Arguments[0], symbolTable);
                        var collectionProjection = new CollectionProjection(value.Matches, value.Projection);
                        return collectionProjection;
                    }
                }
                else if (expression.Type.IsGenericType && expression.Type.GetGenericTypeDefinition() == typeof(IQueryable<>))
                {
                    var value = ProcessQueryable(expression, symbolTable);
                    var collectionProjection = new CollectionProjection(value.Matches, value.Projection);
                    return collectionProjection;
                }
            }
            throw new SpecificationException($"Unsupported type of projection {expression}.");
        }

        private Value ProcessScalar(ImmutableList<Match> matches, Expression expression, SymbolTable symbolTable, string recommendedLabel = "unknown")
        {
            if (expression is ParameterExpression parameterExpression)
            {
                return symbolTable.Get(parameterExpression.Name).Merge(matches);
            }
            else if (expression is NewExpression newExpression)
            {
                var names = newExpression.Members != null
                    ? newExpression.Members.Select(member => member.Name)
                    : newExpression.Constructor.GetParameters().Select(parameter => parameter.Name);
                var values = newExpression.Arguments
                    .Select(arg => ProcessScalar(matches, arg, symbolTable));
                var fields = names.Zip(values, (name, value) => KeyValuePair.Create(name, value.Projection))
                    .ToImmutableDictionary();
                var compoundProjection = new CompoundProjection(fields);
                var value = new Value(matches, compoundProjection);
                return value;
            }
            else if (expression is MemberInitExpression memberInit)
            {
                var fields = memberInit.Bindings
                    .Select(binding => ProcessMemberBinding(matches, binding, symbolTable))
                    .ToImmutableDictionary();
                var compoundProjection = new CompoundProjection(fields);
                var value = new Value(matches, compoundProjection);
                return value;
            }
            else if (expression is MemberExpression memberExpression)
            {
                // Look up each member access.
                var (projection, roles) = ProcessJoinExpression(memberExpression, symbolTable);

                // These should not be predecessor references.
                if (roles.Any())
                {
                    var rolePath = roles.Select(r => r.Name).Join(".");
                    throw new SpecificationException($"Cannot select {rolePath} directly. Give the fact a label first.");
                }
                else
                {
                    return new Value(matches, projection);
                }
            }
            else if (expression is MethodCallExpression observableSpecificationCallExpression &&
                observableSpecificationCallExpression.Method.DeclaringType == typeof(FactRepository) &&
                observableSpecificationCallExpression.Method.Name == nameof(FactRepository.Observable) &&
                observableSpecificationCallExpression.Arguments.Count == 2 &&
                typeof(Specification).IsAssignableFrom(observableSpecificationCallExpression.Arguments[1].Type))
            {
                var start = ProcessScalar(matches, observableSpecificationCallExpression.Arguments[0], symbolTable);
                var label = LabelOfProjection(start.Projection);
                var lambdaExpression = Expression.Lambda<Func<object>>(observableSpecificationCallExpression.Arguments[1]);
                var specification = (Specification)lambdaExpression.Compile().Invoke();
                var arguments = ImmutableList.Create(label);
                specification = specification.Apply(arguments);
                var collectionProjection = new CollectionProjection(specification.Matches, specification.Projection);
                return new Value(matches, collectionProjection);
            }
            else if (expression is MethodCallExpression observableIQueryableCallExpression &&
                observableIQueryableCallExpression.Method.DeclaringType == typeof(FactRepository) &&
                observableIQueryableCallExpression.Method.Name == nameof(FactRepository.Observable) &&
                observableIQueryableCallExpression.Arguments.Count == 1 &&
                typeof(IQueryable).IsAssignableFrom(observableIQueryableCallExpression.Arguments[0].Type))
            {
                var value = ProcessQueryableOld(ImmutableList<Match>.Empty, observableIQueryableCallExpression.Arguments[0], symbolTable);
                var collectionProjection = new CollectionProjection(value.Matches, value.Projection);
                return new Value(matches, collectionProjection);
            }
            else
            {
                var value = ProcessQueryableOld(ImmutableList<Match>.Empty, expression, symbolTable, recommendedLabel);
                var collectionProjection = new CollectionProjection(value.Matches, value.Projection);
                return new Value(matches, collectionProjection);
            }
        }

        private SourceContext ProcessQueryable(Expression expression, SymbolTable symbolTable, string recommendedLabel = "unknown")
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
                        var source = ProcessQueryable(methodCallExpression.Arguments[0], symbolTable, parameterName);
                        var childSymbolTable = symbolTable.Set(parameterName, Value.From(source.Projection));
                        var predicate = ProcessPredicate(lambda.Body, childSymbolTable);
                        return LinqProcessor.Where(source, predicate);
                    }
                    else if (methodCallExpression.Method.Name == nameof(System.Linq.Queryable.Select) &&
                        methodCallExpression.Arguments.Count == 2)
                    {
                        var selector = GetLambda(methodCallExpression.Arguments[1]);
                        var parameterName = selector.Parameters[0].Name;
                        var source = ProcessQueryable(methodCallExpression.Arguments[0], symbolTable, parameterName);
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

                        var source = ProcessQueryable(methodCallExpression.Arguments[0], symbolTable, collectionSelectorParameterName);

                        var collectionSelectorSymbolTable = symbolTable.Set(collectionSelectorParameterName, Value.From(source.Projection));
                        var selector = ProcessQueryable(collectionSelector.Body, collectionSelectorSymbolTable, resultSelectorParameterName);

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

        private Value ProcessQueryableOld(ImmutableList<Match> matches, Expression expression, SymbolTable symbolTable, string recommendedLabel = "unknown")
        {
            if (expression is MethodCallExpression methodCallExpression)
            {
                if (methodCallExpression.Method.DeclaringType == typeof(Queryable))
                {
                    if (methodCallExpression.Method.Name == nameof(System.Linq.Queryable.Where) &&
                        methodCallExpression.Arguments.Count == 2)
                    {
                        var predicate = GetLambda(methodCallExpression.Arguments[1]);
                        var parameterName = predicate.Parameters[0].Name;
                        var source = ProcessQueryableOld(matches, methodCallExpression.Arguments[0], symbolTable, parameterName);
                        var childSymbolTable = symbolTable.Set(parameterName, source);
                        return ProcessWhere(source, predicate.Body, childSymbolTable);
                    }
                    else if (methodCallExpression.Method.Name == nameof(System.Linq.Queryable.Select) &&
                        methodCallExpression.Arguments.Count == 2)
                    {
                        var selector = GetLambda(methodCallExpression.Arguments[1]);
                        var parameterName = selector.Parameters[0].Name;
                        var source = ProcessQueryableOld(matches, methodCallExpression.Arguments[0], symbolTable, parameterName);
                        var childSymbolTable = symbolTable.Set(parameterName, source);
                        return ProcessScalar(source.Matches, selector.Body, childSymbolTable);
                    }
                    else if (methodCallExpression.Method.Name == nameof(System.Linq.Queryable.SelectMany) &&
                        methodCallExpression.Arguments.Count == 3)
                    {
                        var collectionSelector = GetLambda(methodCallExpression.Arguments[1]);
                        var collectionSelectorParameterName = collectionSelector.Parameters[0].Name;
                        var resultSelector = GetLambda(methodCallExpression.Arguments[2]);
                        var resultSelectorParameterName = resultSelector.Parameters[1].Name;
                        var source = ProcessQueryableOld(matches, methodCallExpression.Arguments[0], symbolTable, collectionSelectorParameterName);
                        var collectionSelectorSymbolTable = symbolTable.Set(collectionSelectorParameterName, source);
                        var collectionSelectorValue = ProcessQueryableOld(source.Matches, collectionSelector.Body, collectionSelectorSymbolTable, resultSelectorParameterName);
                        var resultSelectorSymbolTable = symbolTable
                            .Set(resultSelector.Parameters[0].Name, source)
                            .Set(resultSelector.Parameters[1].Name, collectionSelectorValue);
                        var result = ProcessScalar(collectionSelectorValue.Matches, resultSelector.Body, resultSelectorSymbolTable);
                        return result;
                    }
                    else
                    {
                        throw new ArgumentException($"Unsupported method call {methodCallExpression.Method.Name} on Queryable.");
                    }
                }
                else if (methodCallExpression.Method.DeclaringType == typeof(FactRepository))
                {
                    if (methodCallExpression.Method.Name == nameof(FactRepository.OfType) &&
                        methodCallExpression.Arguments.Count == 0)
                    {
                        return AllocateLabel(matches, recommendedLabel, methodCallExpression.Method.GetGenericArguments()[0].FactTypeName());
                    }
                    else if (methodCallExpression.Method.Name == nameof(FactRepository.OfType) &&
                        methodCallExpression.Arguments.Count == 1)
                    {
                        // Get the recommended label from the predicate.
                        var predicate = GetLambda(methodCallExpression.Arguments[0]);
                        var parameterName = predicate.Parameters[0].Name;

                        // Allocate a new label as the source of the match.
                        var genericArgument = methodCallExpression.Method.GetGenericArguments()[0];
                        var source = AllocateLabel(matches, parameterName, genericArgument.FactTypeName());

                        // Process the predicate.
                        var childSymbolTable = symbolTable.Set(parameterName, source);
                        return ProcessWhere(source, predicate.Body, childSymbolTable);
                    }
                    else
                    {
                        throw new ArgumentException($"Unsupported method call {methodCallExpression.Method.Name} on FactRepository.");
                    }
                }
                else
                {
                    throw new ArgumentException($"Unsupported method call declaring type {methodCallExpression.Method.DeclaringType.Name}.");
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported expression type {expression.GetType().Name}: {expression}.");
            }
        }

        private Value AllocateLabel(ImmutableList<Match> matches, string parameterName, string factType)
        {
            var label = NewLabel(parameterName, factType);
            var match = new Match(label, ImmutableList<MatchCondition>.Empty);
            var newMatches = matches.Add(match);
            var projection = new SimpleProjection(parameterName);
            var source = new Value(newMatches, projection);
            return source;
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
                        var source = ProcessQueryable(methodCallExpression.Arguments[0], symbolTable);
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

        private Value ProcessWhere(Value source, Expression predicate, SymbolTable symbolTable)
        {
            if (predicate is BinaryExpression { NodeType: ExpressionType.Equal } binary)
            {
                return ProcessJoin(source, binary.Left, binary.Right, symbolTable);
            }
            else if (predicate is MethodCallExpression methodCallExpression &&
                methodCallExpression.Method.DeclaringType == typeof(Enumerable) &&
                methodCallExpression.Method.Name == nameof(System.Linq.Enumerable.Contains) &&
                methodCallExpression.Arguments.Count == 2)
            {
                return ProcessJoin(source, methodCallExpression.Arguments[0], methodCallExpression.Arguments[1], symbolTable);
            }
            else
            {
                return ProcessExistential(source, predicate, symbolTable, true);
            }
        }

        private Value ProcessJoin(Value source, Expression left, Expression right, SymbolTable symbolTable)
        {
            var (projectionLeft, rolesLeft) = ProcessJoinExpression(left, symbolTable);
            var (projectionRight, rolesRight) = ProcessJoinExpression(right, symbolTable);
            var labelLeft = LabelOfProjection(projectionLeft);
            var labelRight = LabelOfProjection(projectionRight);
            
            var match = source.Matches.LastOrDefault(m =>
                m.Unknown.Name == labelLeft || m.Unknown.Name == labelRight);
            if (match == null)
            {
                throw new ArgumentException($"Neither {labelLeft} nor {labelRight} is found in the query.");
            }
            // Swap the roles so that the left is always the unknown.
            if (labelRight == match.Unknown.Name)
            {
                var temp = labelLeft;
                labelLeft = labelRight;
                labelRight = temp;
                var tempRoles = rolesLeft;
                rolesLeft = rolesRight;
                rolesRight = tempRoles;
            }
            var pathCondition = new PathCondition(rolesLeft, labelRight, rolesRight);
            var newMatch = new Match(match.Unknown, match.Conditions.Add(pathCondition));
            var newMatches = source.Matches.Replace(match, newMatch);
            return new Value(newMatches, source.Projection);
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

        private (Projection projection, ImmutableList<Role> roles) ProcessJoinExpression(Expression expression, SymbolTable symbolTable)
        {
            if (expression is ParameterExpression parameterExpression)
            {
                var projection = symbolTable.Get(parameterExpression.Name).Projection;
                return (projection, ImmutableList<Role>.Empty);
            }
            else if (expression is ConstantExpression)
            {
                var projection = symbolTable.Get("this").Projection;
                return (projection, ImmutableList<Role>.Empty);
            }
            else if (expression is MemberExpression memberExpression)
            {
                var (projection, roles) = ProcessJoinExpression(memberExpression.Expression, symbolTable);
                if (projection is CompoundProjection compoundProjection)
                {
                    if (roles.Any())
                    {
                        throw new InvalidOperationException("The role collection should not be populated until we reach a simple projection.");
                    }
                    var innerProjection = compoundProjection.GetProjection(memberExpression.Member.Name);
                    return (innerProjection, roles);
                }
                else if (projection is SimpleProjection simpleProjection)
                {
                    if (memberExpression.Type.IsFactType() || memberExpression.Type.IsArrayOfFactType())
                    {
                        return (projection, roles.Add(new Role(memberExpression.Member.Name, memberExpression.Type.FactTypeName())));
                    }
                    else
                    {
                        if (roles.Any())
                        {
                            var rolePath = roles.Select(r => r.Name).Join(".");
                            throw new SpecificationException($"Cannot select {simpleProjection.Tag}.{rolePath}.{memberExpression.Member.Name} directly. Give the fact a label first.");
                        }
                        var fieldProjection = new FieldProjection(simpleProjection.Tag, memberExpression.Expression.Type, memberExpression.Member.Name);
                        return (fieldProjection, roles);
                    }
                }
                else
                {
                    throw new ArgumentException($"Unsupported projection type {projection.GetType().Name}.");
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported member expression type {expression.GetType().Name} {expression}.");
            }
        }

        private Value ProcessExistential(Value source, Expression predicate, SymbolTable symbolTable, bool exists)
        {
            if (predicate is UnaryExpression { NodeType: ExpressionType.Not, Operand: Expression operand })
            {
                return ProcessExistential(source, operand, symbolTable, !exists);
            }
            if (predicate is MethodCallExpression methodCallExpression)
            {
                if (methodCallExpression.Method.DeclaringType == typeof(Queryable))
                {
                    if (methodCallExpression.Method.Name == nameof(System.Linq.Queryable.Any) &&
                        methodCallExpression.Arguments.Count == 1)
                    {
                        var value = ProcessQueryableOld(ImmutableList<Match>.Empty, methodCallExpression.Arguments[0], symbolTable);

                        // Find the unknown that the condition references.
                        var firstPathCondition = value.Matches
                            .SelectMany(m => m.Conditions)
                            .OfType<PathCondition>()
                            .FirstOrDefault();
                        if (firstPathCondition == null)
                        {
                            throw new ArgumentException($"An existential predicate must be joined to an outer variable: {predicate}.");
                        }
                        var label = firstPathCondition.LabelRight;

                        // Find the match to which this condition applies.
                        var match = source.Matches.FirstOrDefault(m => m.Unknown.Name == label);
                        if (match == null)
                        {
                            throw new ArgumentException($"The predicate references {label} which is not in the query: {predicate}.");
                        }

                        // Add an existential condition to the match.
                        var existentialCondition = new ExistentialCondition(exists, value.Matches);
                        var newMatch = new Match(match.Unknown, match.Conditions.Add(existentialCondition));
                        var newMatches = source.Matches.Remove(match).Add(newMatch);
                        return new Value(newMatches, source.Projection);
                    }
                    else
                    {
                        throw new ArgumentException($"Unsupported method call {methodCallExpression.Method.Name} on Queryable.");
                    }
                }
                else
                {
                    throw new ArgumentException($"Unsupported method call declaring type {methodCallExpression.Method.DeclaringType.Name}: {methodCallExpression}.");
                }
            }
            else if (predicate is UnaryExpression
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
                    var value = ProcessScalar(ImmutableList<Match>.Empty, member.Expression, symbolTable);
                    var childSymbolTable = symbolTable.Set("this", value);
                    return ProcessExistential(source, condition.Body.Body, childSymbolTable, exists);
                }
                else
                {
                    throw new ArgumentException($"A predicate must be a property of type Condition. This one is a {propertyInfo.PropertyType.Name}: {predicate}.");
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported existential predicate type {predicate.GetType().Name}: {predicate}.");
            }
            throw new NotImplementedException();
        }

        private (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) ProcessResult<TProjection>(Value result)
        {
            var matches = result.Matches;
            ValidateMatches(matches);

            var projection = result.Projection;
            return (givenLabels, matches, projection);
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

        private KeyValuePair<string, Projection> ProcessMemberBinding(ImmutableList<Match> matches, MemberBinding binding, SymbolTable symbolTable)
        {
            if (binding is MemberAssignment assignment)
            {
                var name = assignment.Member.Name;
                var value = ProcessScalar(matches, assignment.Expression, symbolTable);
                return KeyValuePair.Create(name, value.Projection);
            }
            else
            {
                throw new NotImplementedException();
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
