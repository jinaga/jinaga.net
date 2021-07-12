using System.Reflection;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Repository;
using Jinaga.Definitions;
using System.Collections.Generic;

namespace Jinaga.Parsers
{
    public static class SpecificationParser
    {
        public static SymbolValue ParseSpecification(SymbolTable symbolTable, Expression body)
        {
            if (body is MethodCallExpression methodCall)
            {
                var method = methodCall.Method;

                if (method.DeclaringType == typeof(FactRepository) &&
                    method.Name == nameof(FactRepository.OfType))
                {
                    var factType = method.GetGenericArguments()[0].FactTypeName();

                    var set = FactsOfType(factType);
                    return new SymbolValueSetDefinition(set);
                }
                else if (method.DeclaringType == typeof(Queryable))
                {
                    if (method.Name == nameof(Queryable.Where) && methodCall.Arguments.Count == 2)
                    {
                        var source = ParseSpecification(symbolTable, methodCall.Arguments[0]);
                        return ParseWhere(source, symbolTable, methodCall.Arguments[1]);
                    }
                    else if (method.Name == nameof(Queryable.Select) && methodCall.Arguments.Count == 2)
                    {
                        var source = ParseSpecification(symbolTable, methodCall.Arguments[0]);
                        return ParseSelect(source, symbolTable, methodCall.Arguments[1]);
                    }
                    else if (method.Name == nameof(Queryable.SelectMany) && methodCall.Arguments.Count == 3)
                    {
                        var source = ParseSpecification(symbolTable, methodCall.Arguments[0]);
                        return ParseSelectMany(source, symbolTable, methodCall.Arguments[1], methodCall.Arguments[2]);
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

        private static SymbolValue ParseWhere(SymbolValue symbolValue, SymbolTable symbolTable, Expression predicate)
        {
            if (predicate is UnaryExpression {
                Operand: LambdaExpression {
                    Body: BinaryExpression {
                        NodeType: ExpressionType.Equal
                    } binary
                } equalLambda
            })
            {
                var parameterName = equalLambda.Parameters[0].Name;
                var innerSymbolTable = symbolTable.With(parameterName, symbolValue);
                var (left, leftTag) = ValueParser.ParseValue(innerSymbolTable, binary.Left);
                var (right, rightTag) = ValueParser.ParseValue(innerSymbolTable, binary.Right);
                switch (left, right)
                {
                    case (SymbolValueSetDefinition leftSet, SymbolValueSetDefinition rightSet):
                        var leftChain = leftSet.SetDefinition.ToChain();
                        var rightChain = rightSet.SetDefinition.ToChain();
                        (Chain head, Chain tail) = OrderChains(leftChain, rightChain);
                        string tag = (tail == leftChain) ? leftTag : rightTag;
                        var join = new SetDefinitionJoin(tag, head, tail);
                        var target = tail.TargetSetDefinition;
                        return ReplaceSetDefinition(symbolValue, target, join);
                    default:
                        throw new NotImplementedException();
                }
            }
            else if (predicate is UnaryExpression { Operand: LambdaExpression unaryLambda })
            {
                var parameterName = unaryLambda.Parameters[0].Name;
                var innerSymbolTable = symbolTable.With(parameterName, symbolValue);
                var body = unaryLambda.Body;

                var conditionDefinition = ParseCondition(innerSymbolTable, body);
                switch (symbolValue)
                {
                    case SymbolValueSetDefinition setValue:
                        var setDefinition = new SetDefinitionConditional(setValue.SetDefinition, conditionDefinition);
                        return new SymbolValueSetDefinition(setDefinition);
                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
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

        private static SymbolValue ParseSelect(SymbolValue symbolValue, SymbolTable symbolTable, Expression selector)
        {
            if (selector is UnaryExpression {
                Operand: LambdaExpression projectionLambda
            })
            {
                var valueParameterName = projectionLambda.Parameters[0].Name;
                var innerSymbolTable = symbolTable
                    .With(valueParameterName, symbolValue);

                var (value, _) = ValueParser.ParseValue(innerSymbolTable, projectionLambda.Body);
                return value;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static SymbolValue ParseSelectMany(SymbolValue symbolValue, SymbolTable symbolTable, Expression collectionSelector, Expression resultSelector)
        {
            if (collectionSelector is UnaryExpression { Operand: LambdaExpression lambda })
            {
                var parameterName = lambda.Parameters[0].Name;
                var innerSymbolTable = symbolTable.With(parameterName, symbolValue);
                var continuation = ParseSpecification(innerSymbolTable, lambda.Body);
                var projection = ParseProjection(symbolTable, symbolValue, continuation, resultSelector);
                return projection;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static ConditionDefinition ParseCondition(SymbolTable symbolTable, Expression body)
        {
            if (body is UnaryExpression { NodeType: ExpressionType.Not, Operand: Expression operand })
            {
                return ParseCondition(symbolTable, operand).Invert();
            }
            else if (body is MethodCallExpression methodCall)
            {
                var method = methodCall.Method;
                if (method.DeclaringType == typeof(Queryable))
                {
                    if (method.Name == nameof(Queryable.Any) && methodCall.Arguments.Count == 1)
                    {
                        var predicate = methodCall.Arguments[0];
                        var value = ParseSpecification(symbolTable, predicate);
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
                    string factType = propertyInfo.DeclaringType.FactTypeName();
                    var innerSymbolTable = SymbolTable.WithParameter("this", factType);
                    return ParseCondition(innerSymbolTable, condition.Body.Body);
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

        private static SymbolValue ParseProjection(SymbolTable symbolTable, SymbolValue symbolValue, SymbolValue continuation, Expression resultSelector)
        {
            if (resultSelector is UnaryExpression {
                Operand: LambdaExpression projectionLambda
            })
            {
                var valueParameterName = projectionLambda.Parameters[0].Name;
                var continuationParameterName = projectionLambda.Parameters[1].Name;
                var innerSymbolTable = symbolTable
                    .With(valueParameterName, symbolValue)
                    .With(continuationParameterName, continuation);

                return ValueParser.ParseValue(innerSymbolTable, projectionLambda.Body).symbolValue;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static object InstanceOfFact(Type factType)
        {
            var constructor = factType.GetConstructors().First();
            var parameters = constructor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Select(type => type.IsValueType ? Activator.CreateInstance(type) : InstanceOfFact(type))
                .ToArray();
            return Activator.CreateInstance(factType, parameters);
        }

        private static SetDefinition FactsOfType(string factType)
        {
            return new SetDefinitionTarget(factType);
        }

        private static ConditionDefinition Exists(SetDefinition set)
        {
            return new ConditionDefinition(set, exists: true);
        }
    }
}
