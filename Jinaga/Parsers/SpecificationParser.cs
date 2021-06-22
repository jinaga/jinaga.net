using System.Reflection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Pipelines;
using Jinaga.Repository;
using Jinaga.Definitions;

namespace Jinaga.Parsers
{
    public static class SpecificationParser
    {
        public static SetDefinition ParseSpecification(string parameterName, string parameterType, Expression body)
        {
            if (body is MethodCallExpression methodCall)
            {
                var method = methodCall.Method;

                if (method.DeclaringType == typeof(FactRepository) &&
                    method.Name == nameof(FactRepository.OfType))
                {
                    var factType = method.GetGenericArguments()[0].FactTypeName();

                    return FactsOfType(factType);
                }
                else if (method.DeclaringType == typeof(Queryable))
                {
                    if (method.Name == nameof(Queryable.Where) && methodCall.Arguments.Count == 2)
                    {
                        return ParseWhere(ParseSpecification(parameterName, parameterType, methodCall.Arguments[0]), methodCall.Arguments[1]);
                    }
                    else if (method.Name == nameof(Queryable.Select) && methodCall.Arguments.Count == 2)
                    {
                        return ParseSelect(ParseSpecification(parameterName, parameterType, methodCall.Arguments[0]), methodCall.Arguments[1]);
                    }
                    else if (method.Name == nameof(Queryable.SelectMany) && methodCall.Arguments.Count == 3)
                    {
                        return ParseSelectMany(ParseSpecification(parameterName, parameterType, methodCall.Arguments[0]), methodCall.Arguments[1], methodCall.Arguments[2]);
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

        private static SetDefinition ParseWhere(SetDefinition set, Expression predicate)
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
                
                var (startingTag, steps) = JoinSegments(parameterName, binary.Left, binary.Right);
                var stepsDefinition = new StepsDefinition(parameterName, startingTag, steps);

                return set.WithSteps(stepsDefinition);
            }
            else if (predicate is UnaryExpression { Operand: LambdaExpression unaryLambda })
            {
                var parameterName = unaryLambda.Parameters[0].Name;
                var parameterType = unaryLambda.Parameters[0].Type.FactTypeName();
                var body = unaryLambda.Body;

                return set.WithCondition(ParseCondition(parameterName, parameterType, body));
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static SetDefinition ParseSelect(SetDefinition set, Expression selector)
        {
            throw new NotImplementedException();
        }

        private static SetDefinition ParseSelectMany(SetDefinition set, Expression collectionSelector, Expression resultSelector)
        {
            if (collectionSelector is UnaryExpression { Operand: LambdaExpression lambda })
            {
                var parameterName = lambda.Parameters[0].Name;
                var parameterType = lambda.Parameters[0].Type.FactTypeName();
                var body = lambda.Body;
                var continuation = ParseSpecification(parameterName, parameterType, lambda.Body);
                var projection = ParseProjection(resultSelector);
                return set.Compose(continuation, projection);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static ConditionDefinition ParseConditionPredicate(Expression predicate)
        {
            if (predicate is UnaryExpression { Operand: LambdaExpression lambda })
            {
                var parameterName = lambda.Parameters[0].Name;
                var parameterType = lambda.Parameters[0].Type.FactTypeName();
                var body = lambda.Body;

                return ParseCondition(parameterName, parameterType, body);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static ConditionDefinition ParseCondition(string parameterName, string parameterType, Expression body)
        {
            if (body is UnaryExpression { NodeType: ExpressionType.Not, Operand: Expression operand })
            {
                return ParseCondition(parameterName, parameterType, operand).Invert();
            }
            else if (body is MethodCallExpression methodCall)
            {
                var method = methodCall.Method;
                if (method.DeclaringType == typeof(Queryable))
                {
                    if (method.Name == nameof(Queryable.Any) && methodCall.Arguments.Count == 1)
                    {
                        var predicate = methodCall.Arguments[0];
                        var set = ParseSpecification(parameterName, parameterType, predicate);
                        return Exists(set);
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
                    return ParseCondition(parameterName, parameterType, condition.Body.Body);
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

        private static ProjectionDefinition ParseProjection(Expression resultSelector)
        {
            if (resultSelector is UnaryExpression {
                Operand: LambdaExpression {
                    Body: NewExpression newBody
                } projectionLambda
            })
            {
                var parameter0 = projectionLambda.Parameters[0].Name;
                var parameter1 = projectionLambda.Parameters[1].Name;
                var argument0 = ((ParameterExpression)newBody.Arguments[0]).Name;
                var argument1 = ((ParameterExpression)newBody.Arguments[1]).Name;
                var fields = new FieldDefinition[]
                {
                    new FieldDefinition(parameter0, 0),
                    new FieldDefinition(parameter1, 1)
                }.ToImmutableList();
                return new ProjectionDefinition(fields);
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

        private static Path ParseSegmentPredicate(Expression predicate)
        {
            if (predicate is UnaryExpression {
                Operand: LambdaExpression {
                    Body: BinaryExpression {
                        NodeType: ExpressionType.Equal
                    } binary
                } lambda
            })
            {
                var parameterName = lambda.Parameters[0].Name;
                var parameterType = lambda.Parameters[0].Type.FactTypeName();
                
                var (startingTag, steps) = JoinSegments(parameterName, binary.Left, binary.Right);

                var path = new Path(parameterName, parameterType, startingTag, steps);

                return path;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static (string, ImmutableList<Step>) JoinSegments(string parameterName, Expression left, Expression right)
        {
            var (leftRootName, leftSteps) = SegmentParser.ParseSegment(left);
            var (rightRootName, rightSteps) = SegmentParser.ParseSegment(right);

            if (leftRootName == parameterName)
            {
                return (rightRootName, rightSteps.AddRange(ReflectAll(leftSteps)));
            }
            else if (rightRootName == parameterName)
            {
                return (leftRootName, leftSteps.AddRange(ReflectAll(rightSteps)));
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static IEnumerable<Step> ReflectAll(ImmutableList<Step> steps)
        {
            return steps.Reverse().Select(step => step.Reflect()).ToImmutableList();
        }

        private static SetDefinition FactsOfType(string factType)
        {
            return new SimpleSetDefinition(factType);
        }

        private static ConditionDefinition Exists(SetDefinition set)
        {
            return new ConditionDefinition(set, exists: true);
        }
    }
}
