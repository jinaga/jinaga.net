using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Projections;
using Jinaga.Pipelines;
using System.Collections.Immutable;
using Jinaga.Parsers;
using System.Collections.Generic;

namespace Jinaga.Repository
{
    class SpecificationProcessor
    {
        private ImmutableList<Label> labels = ImmutableList<Label>.Empty;
        private ImmutableList<Label> givenLabels = ImmutableList<Label>.Empty;

        private SpecificationProcessor()
        {
        }

        public static (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Queryable<TProjection>(LambdaExpression specExpression)
        {
            var processor = new SpecificationProcessor();
            processor.AddParameters(specExpression.Parameters
                .Take(specExpression.Parameters.Count - 1));
            var factRepository = new FactRepository(processor);
            var result = processor.ProcessExpression(specExpression.Body, "fact");
            return processor.Process<TProjection>(result);
        }

        private void AddParameters(IEnumerable<ParameterExpression> parameters)
        {
            foreach (var parameter in parameters)
            {
                var source = NewLabel(parameter.Name, parameter.Type.FactTypeName());
                AddGiven(source);
            }
        }

        public static (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Scalar<TProjection>(LambdaExpression specExpression)
        {
            throw new NotImplementedException();
        }

        private Label NewLabel(string recommendedName, string factType)
        {
            var source = new Label(recommendedName, factType);
            labels = labels.Add(source);
            return source;
        }

        private void AddGiven(Label label)
        {
            givenLabels = givenLabels.Add(label);
        }

        private Value ProcessExpression(Expression expression, string recommendedLabel)
        {
            if (expression is MethodCallExpression methodCallExpression)
            {
                if (methodCallExpression.Method.DeclaringType == typeof(Queryable))
                {
                    if (methodCallExpression.Method.Name == nameof(System.Linq.Queryable.Where) &&
                        methodCallExpression.Arguments.Count == 2)
                    {
                        var predicate =
                            methodCallExpression.Arguments[1] is UnaryExpression unaryExpression &&
                            unaryExpression.Operand is LambdaExpression lambdaExpression ?
                                lambdaExpression :
                                throw new NotImplementedException();
                        var childRecommendedLabel = predicate.Parameters[0].Name;
                        var source = ProcessExpression(methodCallExpression.Arguments[0], childRecommendedLabel);
                        return ProcessWhere(source, predicate.Body);
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
                        var factType = methodCallExpression.Method.GetGenericArguments()[0].FactTypeName();
                        var source = NewLabel(recommendedLabel, factType);
                        var projection = new SimpleProjection(recommendedLabel);
                        return new Value(projection);
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

        private Value ProcessWhere(Value source, Expression predicate)
        {
            if (predicate is BinaryExpression { NodeType: ExpressionType.Equal } binary)
            {
                return ProcessJoin(source, binary.Left, binary.Right);
            }
            else
            {
                throw new ArgumentException($"Unsupported where predicate {predicate}.");
            }
        }

        private Value ProcessJoin(Value source, Expression left, Expression right)
        {
            return source;
        }

        private (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Process<TProjection>(Value result)
        {
            var matches = ImmutableList<Match>.Empty;
            var projection = result.Projection;
            return (givenLabels, matches, projection);
        }
    }
}
