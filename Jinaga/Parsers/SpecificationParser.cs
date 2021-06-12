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
        public static (ImmutableList<Path>, Projection) ParseSpecification(Expression body)
        {
            if (body is MethodCallExpression methodCall)
            {
                var method = methodCall.Method;
                if (method.DeclaringType == typeof(Queryable))
                {
                    if (method.Name == "Where")
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

        private static (ImmutableList<Path>, Projection) VisitWhere(Expression collection, Expression predicate)
        {
            if (collection is MethodCallExpression methodCall)
            {
                var method = methodCall.Method;

                if (method.DeclaringType == typeof(FactRepository))
                {
                    if (method.Name == nameof(FactRepository.OfType))
                    {
                        var factTypeName = method.GetGenericArguments()[0].FactTypeName();

                        return ParsePredicate(predicate);
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

        private static (ImmutableList<Path>, Projection) ParsePredicate(Expression predicate)
        {
            if (predicate is UnaryExpression unary)
            {
                var operand = unary.Operand;
                if (operand is LambdaExpression lambda)
                {
                    var parameterName = lambda.Parameters[0].Name;
                    var parameterType = lambda.Parameters[0].Type.FactTypeName();
                    
                    if (lambda.Body is BinaryExpression binary)
                    {
                        if (binary.NodeType == ExpressionType.Equal)
                        {
                            var (startingTag, steps) = VisitEqual(parameterName, binary.Left, binary.Right);

                            var path = new Path(parameterName, parameterType, startingTag, steps);
                            var paths = ImmutableList<Path>.Empty.Add(path);
                            var projection = new Projection(parameterName);

                            return (paths, projection);
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
            else
            {
                throw new NotImplementedException();
            }
        }

        private static (string, ImmutableList<Step>) VisitEqual(string parameterName, Expression left, Expression right)
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
