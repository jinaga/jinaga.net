using Jinaga.Repository;
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
            var initial = SingularVariableAndParameter(variable, parameter);
            var queue = ImmutableQueue<(CodeExpressionVisitor visitor, int depth)>.Empty
                .Enqueue((initial, 0));

            while (!queue.IsEmpty)
            {
                var next = queue.Peek();
                queue = queue.Dequeue();

                var recommendation = next.visitor.Recommend();
                if (recommendation != null)
                {
                    return recommendation;
                }
                if (next.depth < MaxDepth)
                {
                    foreach (var visitor in next.visitor.Generate())
                    {
                        queue = queue.Enqueue((visitor, next.depth + 1));
                    }
                }
            }

            return null;
        }

        private static CodeExpressionVisitor SingularVariableAndParameter(CodeExpression variable, CodeExpression parameter)
        {
            return new CodeExpressionVisitor(
                () => RecommendSingularVariableAndParameter(variable, parameter),
                () => VisitSingularVariableAndParameter(variable, parameter)
            );
        }

        private static string? RecommendSingularVariableAndParameter(CodeExpression variable, CodeExpression parameter)
        {
            if (variable.Type == parameter.Type)
            {
                return $"{variable.Code} == {parameter.Code}";
            }
            else
            {
                return null;
            }
        }

        private static IEnumerable<CodeExpressionVisitor> VisitSingularVariableAndParameter(CodeExpression variable, CodeExpression parameter)
        {
            var singularVariable =
                from variablePredecessor in SingularPredecessors(variable)
                select SingularVariableAndParameter(variablePredecessor, parameter);
            var multipleVariable =
                from variablePredecessor in MultiplePredecessors(variable)
                select MultipleVariableAndSingularParameter(variablePredecessor, parameter);
            var singularParameter =
                from parameterPredecessor in SingularPredecessors(parameter)
                select SingularVariableAndParameter(variable, parameterPredecessor);
            var multipleParameter =
                from parameterPredecessor in MultiplePredecessors(parameter)
                select SingularVariableAndMultipleParameter(variable, parameterPredecessor);
            return singularVariable.Concat(multipleVariable).Concat(singularParameter).Concat(multipleParameter);
        }

        private static CodeExpressionVisitor MultipleVariableAndSingularParameter(CodeExpression variable, CodeExpression parameter)
        {
            return new CodeExpressionVisitor(
                () => RecommendMultipleVariableAndSingularParameter(variable, parameter),
                () => VisitMultipleVariableAndSingularParameter(variable, parameter)
            );
        }

        private static string? RecommendMultipleVariableAndSingularParameter(CodeExpression variable, CodeExpression parameter)
        {
            if (variable.Type == parameter.Type)
            {
                return $"{variable.Code}.Contains({parameter.Code})";
            }
            else
            {
                return null;
            }
        }

        private static IEnumerable<CodeExpressionVisitor> VisitMultipleVariableAndSingularParameter(CodeExpression variable, CodeExpression parameter)
        {
            var singularParameter =
                from parameterPredecessor in SingularPredecessors(parameter)
                select MultipleVariableAndSingularParameter(variable, parameterPredecessor);
            return singularParameter;
        }

        private static CodeExpressionVisitor SingularVariableAndMultipleParameter(CodeExpression variable, CodeExpression parameter)
        {
            return new CodeExpressionVisitor(
                () => RecommendSingularVariableAndMultipleParameter(variable, parameter),
                () => VisitSingularVariableAndMultipleParameter(variable, parameter)
            );
        }

        private static string? RecommendSingularVariableAndMultipleParameter(CodeExpression variable, CodeExpression parameter)
        {
            if (variable.Type == parameter.Type)
            {
                return $"{parameter.Code}.Contains({variable.Code})";
            }
            else
            {
                return null;
            }
        }

        private static IEnumerable<CodeExpressionVisitor> VisitSingularVariableAndMultipleParameter(CodeExpression variable, CodeExpression parameter)
        {
            var singularVariable =
                from variablePredecessor in SingularPredecessors(variable)
                select SingularVariableAndMultipleParameter(variablePredecessor, parameter);
            return singularVariable;
        }

        private static IEnumerable<CodeExpression> SingularPredecessors(CodeExpression expression)
        {
            return expression.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.PropertyType.IsFactType())
                .Select(property => new CodeExpression(property.PropertyType, $"{expression.Code}.{property.Name}"));
        }

        private static IEnumerable<CodeExpression> MultiplePredecessors(CodeExpression expression)
        {
            return expression.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.PropertyType.IsArrayOfFactType())
                .Select(property => new CodeExpression(property.PropertyType.GetElementType(), $"{expression.Code}.{property.Name}"));
        }
    }
}
