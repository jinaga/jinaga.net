using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;

namespace Jinaga.Repository
{
    /// <summary>
    /// Collects the predicates written on <c>Given&lt;...&gt;.Where</c>.
    ///
    /// A condition on a given depends on the label of that given and on the labels before it,
    /// so a conjunct that names no given at all belongs nowhere. That is refused here, where
    /// the caller wrote it, rather than later when the specification's body supplies the
    /// labels.
    /// </summary>
    internal static class GivenCondition
    {
        public static ImmutableList<LambdaExpression> Add(
            ImmutableList<LambdaExpression> conditions,
            LambdaExpression condition)
        {
            var givens = condition.Parameters
                .Where(parameter => parameter.Type != typeof(FactRepository))
                .ToImmutableList();
            foreach (var conjunct in SpecificationProcessor.Conjuncts(condition.Body))
            {
                if (!givens.Any(given => References(conjunct, given)))
                {
                    throw new SpecificationException($"The condition {conjunct} does not reference any given. A condition on a given must constrain that given.");
                }
            }
            return conditions.Add(condition);
        }

        private static bool References(Expression expression, ParameterExpression parameter)
        {
            var finder = new ParameterFinder(parameter);
            finder.Visit(expression);
            return finder.Found;
        }

        private class ParameterFinder : ExpressionVisitor
        {
            private readonly ParameterExpression parameter;

            public ParameterFinder(ParameterExpression parameter)
            {
                this.parameter = parameter;
            }

            public bool Found { get; private set; }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == parameter)
                {
                    Found = true;
                }
                return base.VisitParameter(node);
            }
        }
    }
}
