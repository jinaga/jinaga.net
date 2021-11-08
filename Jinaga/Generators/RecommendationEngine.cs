using Jinaga.Parsers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Jinaga.Generators
{
    static class RecommendationEngine
    {
        const int MaxDepth = 5;

        public static string? RecommendJoin(CodeExpression variable, CodeExpression parameter)
        {
            var queue = ImmutableQueue<(CodeExpression variable, CodeExpression parameter, int depth)>.Empty.Enqueue((variable, parameter, 0));

            while (!queue.IsEmpty)
            {
                var next = queue.Peek();
                queue = queue.Dequeue();

                if (next.variable.Type == next.parameter.Type)
                {
                    return $"Consider \"where {next.variable.Code} == {next.parameter.Code}\".";
                }
                if (next.depth < MaxDepth)
                {
                    foreach (var variablePredecessor in SingularPredecessors(next.variable))
                    {
                        queue = queue.Enqueue((variablePredecessor, next.parameter, next.depth + 1));
                    }
                    foreach (var parameterPredecessor in SingularPredecessors(next.parameter))
                    {
                        queue = queue.Enqueue((next.variable, parameterPredecessor, next.depth + 1));
                    }
                }
            }

            return null;
        }

        private static IEnumerable<CodeExpression> SingularPredecessors(CodeExpression expression)
        {
            return expression.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.PropertyType.IsFactType())
                .Select(property => new CodeExpression(property.PropertyType, $"{expression.Code}.{property.Name}"));
        }
    }
}
