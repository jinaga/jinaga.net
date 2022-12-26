using Jinaga.Parsers;
using Jinaga.Pipelines;
using Jinaga.Projections;
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
            var givenParameters = specExpression.Parameters
                .Take(specExpression.Parameters.Count - 1);
            processor.AddParameters(givenParameters);
            var symbolTable = givenParameters.Aggregate(
                SymbolTable.Empty,
                (table, parameter) => table.Set(parameter.Name, Value.Simple(parameter.Name)));
            var result = processor.ProcessExpression(specExpression.Body, symbolTable, "fact");
            return processor.ProcessResult<TProjection>(result);
        }

        public static (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) Scalar<TProjection>(LambdaExpression specExpression)
        {
            var processor = new SpecificationProcessor();
            var givenParameters = specExpression.Parameters;
            processor.AddParameters(givenParameters);
            var symbolTable = givenParameters.Aggregate(
                SymbolTable.Empty,
                (table, parameter) => table.Set(parameter.Name, Value.Simple(parameter.Name)));
            var result = processor.ProcessExpression(specExpression.Body, symbolTable, "fact");
            return processor.ProcessResult<TProjection>(result);
        }

        private void AddParameters(IEnumerable<ParameterExpression> parameters)
        {
            foreach (var parameter in parameters)
            {
                var source = NewLabel(parameter.Name, parameter.Type.FactTypeName());
                AddGiven(source);
            }
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

        private Value ProcessExpression(Expression expression, SymbolTable symbolTable, string recommendedLabel)
        {
            if (expression is ParameterExpression parameterExpression)
            {
                return symbolTable.Get(parameterExpression.Name);
            }
            else if (expression is MethodCallExpression methodCallExpression)
            {
                if (methodCallExpression.Method.DeclaringType == typeof(Queryable))
                {
                    if (methodCallExpression.Method.Name == nameof(System.Linq.Queryable.Where) &&
                        methodCallExpression.Arguments.Count == 2)
                    {
                        var predicate = GetLambda(methodCallExpression.Arguments[1]);
                        var parameterName = predicate.Parameters[0].Name;
                        var source = ProcessExpression(methodCallExpression.Arguments[0], symbolTable, parameterName);
                        var childSymbolTable = symbolTable.Set(parameterName, source);
                        return ProcessWhere(source, predicate.Body, childSymbolTable);
                    }
                    else if (methodCallExpression.Method.Name == nameof(System.Linq.Queryable.Select) &&
                        methodCallExpression.Arguments.Count == 2)
                    {
                        var selector = GetLambda(methodCallExpression.Arguments[1]);
                        var parameterName = selector.Parameters[0].Name;
                        var source = ProcessExpression(methodCallExpression.Arguments[0], symbolTable, parameterName);
                        var childSymbolTable = symbolTable.Set(parameterName, source);
                        return ProcessSelect(source, selector.Body, childSymbolTable);
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
                        return AllocateLabel(recommendedLabel, methodCallExpression.Method.GetGenericArguments()[0].FactTypeName());
                    }
                    else if (methodCallExpression.Method.Name == nameof(FactRepository.OfType) &&
                        methodCallExpression.Arguments.Count == 1)
                    {
                        // Get the recommended label from the predicate.
                        var predicate = GetLambda(methodCallExpression.Arguments[0]);
                        var parameterName = predicate.Parameters[0].Name;

                        // Allocate a new label as the source of the match.
                        var genericArgument = methodCallExpression.Method.GetGenericArguments()[0];
                        var source = AllocateLabel(parameterName, genericArgument.FactTypeName());

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
            else if (expression is MemberExpression memberExpression)
            {
                // Trace predecessors up from the source label.
                var (label, roles) = ProcessJoinExpression(memberExpression, symbolTable);

                // Label the tail of the predecessor chain.
                var lastRole = roles.Last();
                var value = AllocateLabel(lastRole.Name, lastRole.TargetType);

                // Add the path condition to the match.
                var match = value.Matches.Last();
                var pathCondition = new PathCondition(ImmutableList<Role>.Empty, label, roles);
                var newMatch = new Match(match.Unknown, match.Conditions.Add(pathCondition));
                var newMatches = value.Matches.Replace(match, newMatch);
                return new Value(newMatches, value.Projection);
            }
            else
            {
                throw new ArgumentException($"Unsupported expression type {expression.GetType().Name}: {expression}.");
            }
        }

        private Value AllocateLabel(string parameterName, string factType)
        {
            var label = NewLabel(parameterName, factType);
            var match = new Match(label, ImmutableList<MatchCondition>.Empty);
            var matches = ImmutableList.Create(match);
            var projection = new SimpleProjection(parameterName);
            var source = new Value(matches, projection);
            return source;
        }

        private Value ProcessWhere(Value source, Expression predicate, SymbolTable symbolTable)
        {
            if (predicate is BinaryExpression { NodeType: ExpressionType.Equal } binary)
            {
                return ProcessJoin(source, binary.Left, binary.Right, symbolTable);
            }
            else if (predicate is UnaryExpression { NodeType: ExpressionType.Not, Operand: Expression operand })
            {
                return ProcessExistential(source, operand, symbolTable, false);
            }
            else
            {
                throw new ArgumentException($"Unsupported where predicate {predicate}.");
            }
        }

        private Value ProcessJoin(Value source, Expression left, Expression right, SymbolTable symbolTable)
        {
            var (labelLeft, rolesLeft) = ProcessJoinExpression(left, symbolTable);
            var (labelRight, rolesRight) = ProcessJoinExpression(right, symbolTable);
            var match = source.Matches.LastOrDefault(m =>
                m.Unknown.Name == labelLeft || m.Unknown.Name == labelRight);
            if (match == null)
            {
                throw new ArgumentException($"Join expression {left} or {right} is not a member of the query.");
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

        private (string label, ImmutableList<Role> roles) ProcessJoinExpression(Expression expression, SymbolTable symbolTable)
        {
            if (expression is MemberExpression memberExpression)
            {
                var (label, roles) = ProcessJoinExpression(memberExpression.Expression, symbolTable);
                var role = new Role(memberExpression.Member.Name, memberExpression.Type.FactTypeName());
                return (label, roles.Add(role));
            }
            else if (expression is ParameterExpression parameterExpression)
            {
                var value = symbolTable.Get(parameterExpression.Name);
                if (value.Projection is SimpleProjection simpleProjection)
                {
                    return (simpleProjection.Tag, ImmutableList<Role>.Empty);
                }
                else
                {
                    throw new ArgumentException($"Join expression {expression} is not a simple projection.");
                }
            }
            else if (expression is ConstantExpression)
            {
                var value = symbolTable.Get("this");
                if (value.Projection is SimpleProjection simpleProjection)
                {
                    return (simpleProjection.Tag, ImmutableList<Role>.Empty);
                }
                else
                {
                    throw new ArgumentException($"Join expression {expression} is not a simple projection.");
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported join expression type {expression.GetType().Name} {expression}.");
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
                        var value = ProcessExpression(methodCallExpression.Arguments[0], symbolTable, "unknown");

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
                    var value = ProcessExpression(member.Expression, symbolTable, "unknown");
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

        private Value ProcessSelect(Value source, Expression selector, SymbolTable symbolTable)
        {
            return ProcessExpression(selector, symbolTable, "unknown");
        }

        private (ImmutableList<Label> given, ImmutableList<Match> matches, Projection projection) ProcessResult<TProjection>(Value result)
        {
            // Look for matches with no path conditions.
            Match? priorMatch = null;
            foreach (var match in result.Matches)
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

            var matches = result.Matches;
            var projection = result.Projection;
            return (givenLabels, matches, projection);
        }

        private static LambdaExpression GetLambda(Expression argument)
        {
            return argument is UnaryExpression unaryExpression &&
                unaryExpression.Operand is LambdaExpression lambdaExpression ?
                    lambdaExpression :
                    throw new ArgumentException($"Expected a unary lambda expression for {argument}.");
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
