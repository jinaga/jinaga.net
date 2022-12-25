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
                        var match = new Match(source, ImmutableList<MatchCondition>.Empty);
                        var matches = ImmutableList.Create(match);
                        var projection = new SimpleProjection(recommendedLabel);
                        return new Value(matches, projection);
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
            var (rootLeft, rolesLeft) = ProcessJoinExpression(left);
            var (rootRight, rolesRight) = ProcessJoinExpression(right);
            var match = source.Matches.LastOrDefault(m =>
                m.Unknown == rootLeft || m.Unknown == rootRight);
            if (match == null)
            {
                throw new ArgumentException($"Join expression {left} or {right} is not a member of the query.");
            }
            // Swap the roles so that the left is always the unknown.
            if (rootRight == match.Unknown)
            {
                var temp = rootLeft;
                rootLeft = rootRight;
                rootRight = temp;
                var tempRoles = rolesLeft;
                rolesLeft = rolesRight;
                rolesRight = tempRoles;
            }
            var pathCondition = new PathCondition(rolesLeft, rootRight.Name, rolesRight);
            var newMatch = new Match(match.Unknown, match.Conditions.Add(pathCondition));
            var newMatches = source.Matches.Replace(match, newMatch);
            return new Value(newMatches, source.Projection);
        }

        private (Label root, ImmutableList<Role> roles) ProcessJoinExpression(Expression expression)
        {
            if (expression is MemberExpression memberExpression)
            {
                var (root, roles) = ProcessJoinExpression(memberExpression.Expression);
                var role = new Role(memberExpression.Member.Name, memberExpression.Type.FactTypeName());
                return (root, roles.Add(role));
            }
            else if (expression is ParameterExpression parameterExpression)
            {
                var root = NewLabel(parameterExpression.Name, parameterExpression.Type.FactTypeName());
                return (root, ImmutableList<Role>.Empty);
            }
            else
            {
                throw new ArgumentException($"Unsupported join expression type {expression.GetType().Name} {expression}.");
            }
        }

        private (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Process<TProjection>(Value result)
        {
            var matches = result.Matches;
            var projection = result.Projection;
            return (givenLabels, matches, projection);
        }
    }
}
