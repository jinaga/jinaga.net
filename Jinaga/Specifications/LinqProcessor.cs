﻿using Jinaga.Pipelines;
using Jinaga.Projections;
using System;
using System.Collections.Immutable;

namespace Jinaga.Specifications
{
    internal class LinqProcessor
    {
        public static SourceContext FactsOfType(Label unknown)
        {
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

        public static PredicateContext Any(SourceContext source)
        {
            ConditionContext existentialContidion = new ExistentialConditionContext(true, source.Matches);
            var conditions = ImmutableList.Create(existentialContidion);
            return new PredicateContext(conditions);
        }

        public static PredicateContext Not(PredicateContext predicate)
        {
            if (predicate.Conditions.Count != 1)
            {
                throw new ArgumentException("Not must have exactly one condition");
            }
            var condition = predicate.Conditions[0];
            if (!(condition is ExistentialConditionContext existentialCondition))
            {
                throw new ArgumentException("Not must have an existential condition");
            }

            ConditionContext newCondition = new ExistentialConditionContext(!existentialCondition.Exists, existentialCondition.Matches);
            var conditions = ImmutableList.Create(newCondition);
            return new PredicateContext(conditions);
        }

        public static SourceContext Where(SourceContext source, PredicateContext predicate)
        {
            var matches = source.Matches;
            foreach (var condition in predicate.Conditions)
            {
                matches = ApplyCondition(condition, matches);
            }
            return new SourceContext(matches, source.Projection);
        }

        private static ImmutableList<Match> ApplyCondition(ConditionContext condition, ImmutableList<Match> matches)
        {
            if (condition is PathConditionContext pathCondition)
            {
                // TODO: Find the latest match with the unknown that matches the path condition.
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
            else if (condition is ExistentialConditionContext existentialCondition)
            {
                // TODO: Find the match with the unknown that matches the existential condition.
                int matchIndex = 0;
                var match = matches[matchIndex];
                var conditions = match.Conditions;
                MatchCondition newCondition = new ExistentialCondition(existentialCondition.Exists, existentialCondition.Matches);
                conditions = conditions.Add(newCondition);
                match = new Match(match.Unknown, conditions);
                return matches
                    .RemoveAt(matchIndex)
                    .Insert(matchIndex, match);
            }
            else
            {
                throw new ArgumentException("Unknown condition type");
            }
        }

        public static SourceContext Select(SourceContext source, Projection selector)
        {
            return new SourceContext(source.Matches, selector);
        }

        public static SourceContext SelectMany(SourceContext source, SourceContext selector)
        {
            var matches = source.Matches.AddRange(selector.Matches);
            return new SourceContext(matches, selector.Projection);
        }
    }
}
