using System.Reflection;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Repository;
using Jinaga.Definitions;
using System.Collections.Generic;
using Jinaga.Pipelines;

namespace Jinaga.Parsers
{
    public static class SpecificationParser
    {
        public static SymbolValue ParseSpecification(SymbolTable symbolTable, SpecificationContext context, Expression body)
        {
            if (body is MethodCallExpression methodCall)
            {
                var method = methodCall.Method;

                if (method.DeclaringType == typeof(FactRepository) &&
                    method.Name == nameof(FactRepository.OfType))
                {
                    var type = method.GetGenericArguments()[0];
                    var factType = type.FactTypeName();

                    var set = FactsOfType(factType, type);
                    var source = new SymbolValueSetDefinition(set);

                    if (methodCall.Arguments.Count == 0)
                    {
                        return source;
                    }
                    else
                    {
                        return ParseWhere(source, symbolTable, context, methodCall.Arguments[0]);
                    }
                }
                else if (method.DeclaringType == typeof(Queryable))
                {
                    if (method.Name == nameof(Queryable.Where) && methodCall.Arguments.Count == 2)
                    {
                        var source = ParseSpecification(symbolTable, context, methodCall.Arguments[0]);
                        return ParseWhere(source, symbolTable, context, methodCall.Arguments[1]);
                    }
                    else if (method.Name == nameof(Queryable.Select) && methodCall.Arguments.Count == 2)
                    {
                        var source = ParseSpecification(symbolTable, context, methodCall.Arguments[0]);
                        return ParseSelect(source, symbolTable, context, methodCall.Arguments[1]);
                    }
                    else if (method.Name == nameof(Queryable.SelectMany) && methodCall.Arguments.Count == 3)
                    {
                        var source = ParseSpecification(symbolTable, context, methodCall.Arguments[0]);
                        return ParseSelectMany(source, symbolTable, context, methodCall.Arguments[1], methodCall.Arguments[2]);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static SymbolValue ParseWhere(SymbolValue symbolValue, SymbolTable symbolTable, SpecificationContext context, Expression predicate)
        {
            switch (predicate)
            {
                case UnaryExpression { Operand: LambdaExpression lambda }:
                    var parameterName = lambda.Parameters[0].Name;
                    var innerSymbolTable = symbolTable.With(parameterName, symbolValue);

                    switch (lambda.Body)
                    {
                        case BinaryExpression { NodeType: ExpressionType.Equal } binary:
                            return ParseJoin(symbolValue, innerSymbolTable, context, binary.Left, binary.Right);
                        case MethodCallExpression methodCall:
                            var method = methodCall.Method;
                            if (method.DeclaringType == typeof(Enumerable)
                                && method.Name == nameof(Enumerable.Contains)
                                && methodCall.Arguments.Count == 2)
                            {
                                return ParseJoin(symbolValue, innerSymbolTable, context, methodCall.Arguments[0], methodCall.Arguments[1]);
                            }
                            else
                            {
                                return ParseConditional(symbolValue, innerSymbolTable, context, lambda.Body);
                            }
                        default:
                            return ParseConditional(symbolValue, innerSymbolTable, context, lambda.Body);
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        private static SymbolValue ParseJoin(SymbolValue symbolValue, SymbolTable innerSymbolTable, SpecificationContext context, Expression leftExpression, Expression rightExpression)
        {
            var (left, leftTag) = ValueParser.ParseValue(innerSymbolTable, context, leftExpression);
            var (right, rightTag) = ValueParser.ParseValue(innerSymbolTable, context, rightExpression);
            switch (left, right)
            {
                case (SymbolValueSetDefinition leftSet, SymbolValueSetDefinition rightSet):
                    var leftChain = leftSet.SetDefinition.ToChain();
                    var rightChain = rightSet.SetDefinition.ToChain();
                    (Chain head, Chain tail) = OrderChains(leftChain, rightChain);
                    string tag = (tail == leftChain) ? leftTag : rightTag;
                    var join = new SetDefinitionJoin(tag, head, tail, tail.SourceType);
                    var target = tail.TargetSetDefinition;
                    var replacement = ReplaceSetDefinition(symbolValue, target, join);
                    return replacement;
                default:
                    throw new NotImplementedException();
            }
        }

        private static SymbolValue ParseConditional(SymbolValue symbolValue, SymbolTable innerSymbolTable, SpecificationContext context, Expression body)
        {
            var conditionDefinition = ParseCondition(symbolValue, innerSymbolTable, context, body);
            var evaluatedSet = FindEvaluatedSet(conditionDefinition);
            var conditionalSetDefinition = new SetDefinitionConditional(evaluatedSet, conditionDefinition, evaluatedSet.Type);
            var replacement = ReplaceSetDefinition(symbolValue, evaluatedSet, conditionalSetDefinition);
            return replacement;
        }

        private static SetDefinition FindEvaluatedSet(ConditionDefinition conditionDefinition)
        {
            switch (conditionDefinition.Set)
            {
                case SetDefinitionConditional conditionalSet:
                    return FindEvaluatedSet(conditionalSet.Condition);
                case SetDefinitionJoin joinSet:
                    return joinSet.Head.TargetSetDefinition;
                default:
                    throw new NotImplementedException();
            }
        }

        private static SymbolValue ReplaceSetDefinition(SymbolValue symbolValue, SetDefinition remove, SetDefinition insert)
        {
            switch (symbolValue)
            {
                case SymbolValueSetDefinition setValue:
                    return setValue.SetDefinition == remove
                        ? new SymbolValueSetDefinition(insert)
                        : symbolValue;
                case SymbolValueComposite compositeValue:
                    var fields = compositeValue.Fields
                        .Select(pair => new KeyValuePair<string, SymbolValue>(pair.Key,
                            ReplaceSetDefinition(pair.Value, remove, insert)))
                        .ToImmutableDictionary();
                    return new SymbolValueComposite(fields);
                default:
                    throw new NotImplementedException();
            }
        }

        private static (Chain head, Chain tail) OrderChains(Chain leftChain, Chain rightChain)
        {
            bool leftIsTarget = leftChain.IsTarget;
            bool rightIsTarget = rightChain.IsTarget;
            if (leftIsTarget && !rightIsTarget)
            {
                return (rightChain, leftChain);
            }
            else if (rightIsTarget && !leftIsTarget)
            {
                return (leftChain, rightChain);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static SymbolValue ParseSelect(SymbolValue symbolValue, SymbolTable symbolTable, SpecificationContext context, Expression selector)
        {
            if (selector is UnaryExpression {
                Operand: LambdaExpression projectionLambda
            })
            {
                var valueParameterName = projectionLambda.Parameters[0].Name;
                var valueParameterType = projectionLambda.Parameters[0].Type;
                var innerSymbolTable = symbolTable
                    .With(valueParameterName, ApplyLabel(symbolValue, valueParameterName, valueParameterType));

                var (value, _) = ValueParser.ParseValue(innerSymbolTable, context, projectionLambda.Body);
                return value;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static SymbolValue ParseSelectMany(SymbolValue symbolValue, SymbolTable symbolTable, SpecificationContext context, Expression collectionSelector, Expression resultSelector)
        {
            if (collectionSelector is UnaryExpression { Operand: LambdaExpression lambda })
            {
                var parameterName = lambda.Parameters[0].Name;
                var parameterType = lambda.Parameters[0].Type;
                var innerSymbolTable = symbolTable.With(
                    parameterName,
                    ApplyLabel(symbolValue, parameterName, parameterType)
                );
                var continuation = ParseSpecification(innerSymbolTable, context, lambda.Body);
                var projection = ParseProjection(symbolTable, context, symbolValue, continuation, resultSelector);
                return projection;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static ConditionDefinition ParseCondition(SymbolValue symbolValue, SymbolTable symbolTable, SpecificationContext context, Expression body)
        {
            if (body is UnaryExpression { NodeType: ExpressionType.Not, Operand: Expression operand })
            {
                return ParseCondition(symbolValue, symbolTable, context, operand).Invert();
            }
            else if (body is MethodCallExpression methodCall)
            {
                var method = methodCall.Method;
                if (method.DeclaringType == typeof(Queryable))
                {
                    if (method.Name == nameof(Queryable.Any) && methodCall.Arguments.Count == 1)
                    {
                        var predicate = methodCall.Arguments[0];
                        var value = ParseSpecification(symbolTable, context, predicate);
                        if (value is SymbolValueSetDefinition setDefinitionValue)
                        {
                            return Exists(setDefinitionValue.SetDefinition);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else if (body is UnaryExpression {
                Operand: MemberExpression {
                    Member: PropertyInfo propertyInfo
                } member,
                NodeType: ExpressionType.Convert
            } unary)
            {
                if (propertyInfo.PropertyType == typeof(Condition) &&
                    unary.Type == typeof(Boolean))
                {
                    object target = InstanceOfFact(propertyInfo.DeclaringType);
                    var condition = (Condition)propertyInfo.GetGetMethod().Invoke(target, new object[0]);
                    var instanceValue = ValueParser.ParseValue(symbolTable, context, member.Expression).symbolValue;
                    var innerSymbolTable = SymbolTable.Empty.With("this", instanceValue);
                    return ParseCondition(symbolValue, innerSymbolTable, context, condition.Body.Body);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static SymbolValue ParseProjection(SymbolTable symbolTable, SpecificationContext context, SymbolValue symbolValue, SymbolValue continuation, Expression resultSelector)
        {
            if (resultSelector is UnaryExpression {
                Operand: LambdaExpression projectionLambda
            })
            {
                var valueParameterName = projectionLambda.Parameters[0].Name;
                var valueParameterType = projectionLambda.Parameters[0].Type;
                var continuationParameterName = projectionLambda.Parameters[1].Name;
                var continuationParameterType = projectionLambda.Parameters[1].Type;
                var innerSymbolTable = symbolTable
                    .With(
                        valueParameterName,
                        ApplyLabel(symbolValue, valueParameterName, valueParameterType))
                    .With(
                        continuationParameterName,
                        ApplyLabel(continuation, continuationParameterName, continuationParameterType));

                return ValueParser.ParseValue(innerSymbolTable, context, projectionLambda.Body).symbolValue;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static SymbolValue ApplyLabel(SymbolValue symbolValue, string name, Type type)
        {
            if (symbolValue is SymbolValueSetDefinition
                {
                    SetDefinition: SetDefinitionTarget setDefinitionTarget
                })
            {
                string factType = type.FactTypeName();
                if (factType != setDefinitionTarget.FactType)
                {
                    throw new SpecificationException($"Parameter mismatch between {factType} and {setDefinitionTarget.FactType}");
                }
                var label = new Label(name, factType);
                return new SymbolValueSetDefinition(new SetDefinitionLabeledTarget(label, type));
            }
            else
            {
                return symbolValue;
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

        private static SetDefinition FactsOfType(string factType, Type type)
        {
            return new SetDefinitionTarget(factType, type);
        }

        private static ConditionDefinition Exists(SetDefinition set)
        {
            return new ConditionDefinition(set, exists: true);
        }
    }
}
