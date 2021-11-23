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
        public static SpecificationResult ParseSpecification(SymbolTable symbolTable, SpecificationContext context, Expression body)
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
                    var sourceSymbolValue = new SymbolValueSetDefinition(set);
                    var source = SpecificationResult.FromValue(sourceSymbolValue)
                        .WithSetDefinition(set);

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
                        throw new SpecificationException($"You cannot use {method.Name} in a Jinaga specification.");
                    }
                }
                else
                {
                    throw new SpecificationException($"You cannot use {method.DeclaringType.Name}.{method.Name} in a Jinaga specification.");
                }
            }
            else
            {
                throw new SpecificationException($"You cannot use the syntax {body} in a Jinaga specification.");
            }
        }

        private static SpecificationResult ParseWhere(SpecificationResult source, SymbolTable symbolTable, SpecificationContext context, Expression predicate)
        {
            if (predicate is UnaryExpression {
                Operand: LambdaExpression lambda
            })
            {
                var parameterName = lambda.Parameters[0].Name;
                var parameterType = lambda.Parameters[0].Type;
                var labeledSource = ApplyLabel(source, parameterName, parameterType)
                    .WithValue(source.SymbolValue);
                var innerSymbolTable = symbolTable.With(parameterName, source.SymbolValue);

                if (lambda.Body is BinaryExpression { NodeType: ExpressionType.Equal } binary)
                {
                    return ParseJoin(labeledSource, innerSymbolTable, context, binary.Left, binary.Right);
                }
                else if (lambda.Body is MethodCallExpression methodCall)
                {
                    var method = methodCall.Method;
                    if (method.DeclaringType == typeof(Enumerable)
                        && method.Name == nameof(Enumerable.Contains)
                        && methodCall.Arguments.Count == 2)
                    {
                        return ParseJoin(labeledSource, innerSymbolTable, context, methodCall.Arguments[0], methodCall.Arguments[1]);
                    }
                    else
                    {
                        return ParseConditional(labeledSource, innerSymbolTable, context, lambda.Body);
                    }
                }
                else
                {
                    return ParseConditional(labeledSource, innerSymbolTable, context, lambda.Body);
                }
            }
            else
            {
                throw new SpecificationException($"The parameter to Where is expected to be a lambda expression. You cannot use the syntax {predicate} in a Jinaga specification.");
            }
        }

        private static SpecificationResult ParseJoin(SpecificationResult source, SymbolTable innerSymbolTable, SpecificationContext context, Expression leftExpression, Expression rightExpression)
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
                    string consumedTag = (head == leftChain) ? leftTag : rightTag;
                    var join = new SetDefinitionJoin(tag, head, tail, tail.SourceType);
                    var target = tail.TargetSetDefinition;
                    var replacement = ReplaceSetDefinition(source.SymbolValue, target, join);
                    return source
                        .ConsumeVariable(consumedTag)
                        .WithSetDefinition(join)
                        .WithValue(replacement);
                default:
                    throw new SpecificationException($"The two sides of a join must be Jinaga facts. You cannot join {left} to {right}.");
            }
        }

        private static SpecificationResult ParseConditional(SpecificationResult source, SymbolTable innerSymbolTable, SpecificationContext context, Expression body)
        {
            var conditionDefinition = ParseCondition(source.SymbolValue, innerSymbolTable, context, body);
            var evaluatedSet = FindEvaluatedSet(conditionDefinition);
            var conditionalSetDefinition = new SetDefinitionConditional(evaluatedSet, conditionDefinition, evaluatedSet.Type);
            var replacement = ReplaceSetDefinition(source.SymbolValue, evaluatedSet, conditionalSetDefinition);
            return source.WithSetDefinition(conditionalSetDefinition).WithValue(replacement);
        }

        private static SetDefinition FindEvaluatedSet(ConditionDefinition conditionDefinition)
        {
            if (conditionDefinition.Set is SetDefinitionConditional conditionalSet)
            {
                return FindEvaluatedSet(conditionalSet.Condition);
            }
            else if (conditionDefinition.Set is SetDefinitionJoin joinSet)
            {
                return joinSet.Head.TargetSetDefinition;
            }
            else
            {
                throw new SpecificationException($"A join must be specified before an existential condition. There is no join in {conditionDefinition}.");
            }
        }

        private static SymbolValue ReplaceSetDefinition(SymbolValue symbolValue, SetDefinition remove, SetDefinition insert)
        {
            if (symbolValue is SymbolValueSetDefinition setValue)
            {
                return setValue.SetDefinition == remove
                    ? new SymbolValueSetDefinition(insert)
                    : symbolValue;
            }
            else if (symbolValue is SymbolValueComposite compositeValue)
            {
                var fields = compositeValue.Fields
                    .Select(pair => new KeyValuePair<string, SymbolValue>(pair.Key,
                        ReplaceSetDefinition(pair.Value, remove, insert)))
                    .ToImmutableDictionary();
                return new SymbolValueComposite(fields);
            }
            else
            {
                throw new SpecificationException($"You cannot use the symbol {symbolValue} in this context.");
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
                throw new SpecificationException($"One side of a join must be a new set, and the other either a parameter or an established set. You cannot join {leftChain} to {rightChain}.");
            }
        }

        private static SpecificationResult ParseSelect(SpecificationResult result, SymbolTable symbolTable, SpecificationContext context, Expression selector)
        {
            if (selector is UnaryExpression {
                Operand: LambdaExpression projectionLambda
            })
            {
                var valueParameterName = projectionLambda.Parameters[0].Name;
                var valueParameterType = projectionLambda.Parameters[0].Type;
                var labeledResult = ApplyLabel(result, valueParameterName, valueParameterType);
                var innerSymbolTable = symbolTable
                    .With(valueParameterName, labeledResult.SymbolValue);

                var (value, _) = ValueParser.ParseValue(innerSymbolTable, context, projectionLambda.Body);
                return labeledResult.WithValue(value);
            }
            else
            {
                throw new SpecificationException($"The parameter to Select is expected to be a lambda expression. You cannot use the syntax {selector} in a Jinaga specification.");
            }
        }

        private static SpecificationResult ParseSelectMany(SpecificationResult result, SymbolTable symbolTable, SpecificationContext context, Expression collectionSelector, Expression resultSelector)
        {
            if (collectionSelector is UnaryExpression { Operand: LambdaExpression lambda })
            {
                var parameterName = lambda.Parameters[0].Name;
                var parameterType = lambda.Parameters[0].Type;
                var labeledResult = ApplyLabel(result, parameterName, parameterType);
                var innerSymbolTable = symbolTable.With(
                    parameterName,
                    labeledResult.SymbolValue
                );
                var continuation = ParseSpecification(innerSymbolTable, context, lambda.Body);
                var projection = ParseProjection(symbolTable, context, labeledResult, continuation, resultSelector);
                return projection;
            }
            else
            {
                throw new SpecificationException($"The parameter to SelectMany is expected to be a lambda expression. You cannot use the syntax {collectionSelector} in a Jinaga specification.");
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
                        if (value.SymbolValue is SymbolValueSetDefinition setDefinitionValue)
                        {
                            return Exists(setDefinitionValue.SetDefinition);
                        }
                        else
                        {
                            throw new SpecificationException($"The parameter to Any must be a lambda that returns a fact. You cannot use {value.SymbolValue} in a Jinaga existential condition.");
                        }
                    }
                    else
                    {
                        throw new SpecificationException($"You cannot use the method {method.Name} in a Jinaga condition.");
                    }
                }
                else
                {
                    throw new SpecificationException($"You cannot use the method {method.DeclaringType.Name}.{method.Name} in a Jinaga condition.");
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
                    throw new SpecificationException($"You cannot use the syntax {body} in a Jinaga condition.");
                }
            }
            else
            {
                throw new SpecificationException($"You cannot use the syntax {body} in a Jinaga condition.");
            }
        }

        private static SpecificationResult ParseProjection(SymbolTable symbolTable, SpecificationContext context, SpecificationResult source, SpecificationResult continuation, Expression resultSelector)
        {
            if (resultSelector is UnaryExpression {
                Operand: LambdaExpression projectionLambda
            })
            {
                var valueParameterName = projectionLambda.Parameters[0].Name;
                var valueParameterType = projectionLambda.Parameters[0].Type;
                var continuationParameterName = projectionLambda.Parameters[1].Name;
                var continuationParameterType = projectionLambda.Parameters[1].Type;
                var labeledSource = ApplyLabel(source, valueParameterName, valueParameterType);
                var labeledContinuation = ApplyLabel(continuation, continuationParameterName, continuationParameterType);
                var innerSymbolTable = symbolTable
                    .With(
                        valueParameterName,
                        labeledSource.SymbolValue
                    )
                    .With(
                        continuationParameterName,
                        labeledContinuation.SymbolValue
                    );

                var value = ValueParser.ParseValue(innerSymbolTable, context, projectionLambda.Body).symbolValue;
                return labeledSource.Compose(labeledContinuation).WithValue(value);
            }
            else
            {
                throw new SpecificationException($"You cannot use the syntax {resultSelector} in a Jinaga projection.");
            }
        }

        private static SpecificationResult ApplyLabel(SpecificationResult result, string name, Type type)
        {
            if (result.SymbolValue is SymbolValueSetDefinition
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
                var symbolValue = new SymbolValueSetDefinition(new SetDefinitionLabeledTarget(label, type));
                return result.WithVariable(label, type).WithValue(symbolValue);
            }
            else
            {
                return result;
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
