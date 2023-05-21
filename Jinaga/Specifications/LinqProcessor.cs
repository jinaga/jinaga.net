using Jinaga.Pipelines;
using Jinaga.Projections;
using System.Collections.Immutable;

namespace Jinaga.Specifications
{
    internal class LinqProcessor
    {
        public static SourceContext FactsOfType(string typeName)
        {
            var unknown = new Label("***", typeName);
            var match = new Match(unknown, ImmutableList<MatchCondition>.Empty);
            var matches = ImmutableList.Create(match);
            var projection = new SimpleProjection(unknown.Name);
            return new SourceContext(matches, projection);
        }

        public static PredicateContext Compare(ReferenceContext left, ReferenceContext right)
        {
            var leftType = left.Roles.Count > 0
                ? left.Roles[left.Roles.Count - 1].TargetType
                : left.Label.Type;
            var rightType = right.Roles.Count > 0
                ? right.Roles[right.Roles.Count - 1].TargetType
                : right.Label.Type;
            if (leftType != rightType)
            {
                throw new System.ArgumentException($"Cannot join {leftType} to {rightType}.");
            }

            ConditionContext pathCondition = new PathConditionContext(left, right);
            var conditions = ImmutableList.Create(pathCondition);
            return new PredicateContext(conditions);
        }
    }
}
