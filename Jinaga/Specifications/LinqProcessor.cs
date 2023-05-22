using Jinaga.Pipelines;
using Jinaga.Projections;
using System;
using System.Collections.Immutable;
using System.Linq;

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

        public static SourceContext Where(SourceContext source, Label newLabel, PredicateContext predicate)
        {
            var replacements = ImmutableDictionary<string, string>.Empty
                .Add("***", newLabel.Name);
            var matches = source.Matches
                .Select(match => match.Apply(replacements))
                .ToImmutableList();
            foreach (var condition in predicate.Conditions)
            {
                matches = ApplyCondition(condition, matches);
            }
            var projection = source.Projection.Apply(replacements);
            return new SourceContext(matches, projection);
        }

        private static ImmutableList<Match> ApplyCondition(ConditionContext condition, ImmutableList<Match> matches)
        {
            if (condition is PathConditionContext pathCondition)
            {
                int matchIndex = 0;
                var match = matches[matchIndex];
                var conditions = match.Conditions;
                MatchCondition newCondition =
                    pathCondition.Left.Label == match.Unknown
                        ? new PathCondition(
                            pathCondition.Left.Roles,
                            pathCondition.Right.Label.Name,
                            pathCondition.Right.Roles) :
                    pathCondition.Right.Label == match.Unknown
                        ? new PathCondition(
                            pathCondition.Right.Roles,
                            pathCondition.Left.Label.Name,
                            pathCondition.Left.Roles) :
                    throw new ArgumentException("The path condition does not match the unknown");
                conditions = conditions.Add(newCondition);
                match = new Match(match.Unknown, conditions);
                return matches
                    .RemoveAt(matchIndex)
                    .Insert(matchIndex, match);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static SourceContext SelectMany(SourceContext source, SourceContext selector, Label newLabel, Projection resultSelector)
        {
            // Replace the unknown in the selector with the new label.
            var replacements = ImmutableDictionary<string, string>.Empty
                .Add("***", newLabel.Name);
            var selectorMatches = selector.Matches
                .Select(match => match.Apply(replacements))
                .ToImmutableList();
            // Concatenate the matches from the source and the selector.
            var matches = source.Matches.AddRange(selectorMatches);
            // Use the result selector to create a new projection.
            var projection = resultSelector;
            return new SourceContext(matches, projection);
        }
    }
}
