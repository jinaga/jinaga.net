using System.Reflection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Pipelines;
using Jinaga.Repository;

namespace Jinaga.Parsers
{
    public static class SpecificationParser
    {
        public static ImmutableList<Path> ParseSpecification(Expression body)
        {
            if (body is MethodCallExpression methodCall)
            {
                var method = methodCall.Method;
                if (method.DeclaringType == typeof(Queryable))
                {
                    if (method.Name == nameof(Queryable.Where))
                    {
                        return VisitWhere(methodCall.Arguments[0], methodCall.Arguments[1]);
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

        private static ImmutableList<Path> VisitWhere(Expression collection, Expression predicate)
        {
            if (collection is MethodCallExpression methodCall)
            {
                var method = methodCall.Method;

                if (method.DeclaringType == typeof(FactRepository) &&
                    method.Name == nameof(FactRepository.OfType))
                {
                    var factTypeName = method.GetGenericArguments()[0].FactTypeName();

                    var path = ParseSegmentPredicate(predicate);
                    var paths = ImmutableList<Path>.Empty.Add(path);
                    return paths;
                }
                else if (method.DeclaringType == typeof(Queryable) &&
                    method.Name == nameof(Queryable.Where))
                {
                    var initialPaths = VisitWhere(methodCall.Arguments[0], methodCall.Arguments[1]);

                    var condition = ParseConditionPredicate(predicate);
                    var initialPath = initialPaths.Single();
                    var path = new Path(initialPath.Tag, initialPath.TargetType, initialPath.StartingTag, initialPath.Steps.Add(
                        condition
                    ));
                    var paths = ImmutableList<Path>.Empty.Add(path);
                    return paths;
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

        private static ConditionalStep ParseConditionPredicate(Expression predicate)
        {
            if (predicate is UnaryExpression { Operand: LambdaExpression lambda })
            {
                var parameterName = lambda.Parameters[0].Name;
                var parameterType = lambda.Parameters[0].Type.FactTypeName();
                var body = lambda.Body;

                if (body is UnaryExpression { NodeType: ExpressionType.Not, Operand: Expression operand })
                {
                    return ParseConditionalStep(operand).Invert();
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

        public static ConditionalStep ParseConditionalStep(Expression body)
        {
            if (body is MethodCallExpression methodCall)
            {
                var method = methodCall.Method;
                if (method.DeclaringType == typeof(Queryable))
                {
                    if (method.Name == nameof(Queryable.Any) && methodCall.Arguments.Count == 1)
                    {
                        var predicate = methodCall.Arguments[0];
                        var paths = ParseSpecification(predicate);
                        var path = paths.Single();
                        var steps = path.Steps;
                        return new ConditionalStep(steps, exists: true);
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
                    return ParseConditionalStep(condition.Body.Body);
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
    }
}
