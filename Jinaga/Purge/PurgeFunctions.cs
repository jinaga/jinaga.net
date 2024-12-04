using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Projections;

internal static class PurgeFunctions
{
    internal static IEnumerable<string> ValidatePurgeSpecification(Specification purgeSpecification)
    {
        // Validate that the specification has only one given.
        if (purgeSpecification.Givens.Count != 1)
        {
            return new[] { "A purge specification must have exactly one given." };
        }
        var purgeRoot = purgeSpecification.Givens[0];

        // Search for negative existential conditions.
        // Those indicate that the specification will reverse a purge.
        var failures = purgeSpecification.Matches.SelectMany(match =>
            match.ExistentialConditions
                .Where(condition => !condition.Exists)
                .Select(condition =>
                    $"A specified purge condition would reverse the purge of {purgeRoot.Label.Type} with {DescribeTuple(condition.Matches)}."
                )
        ).ToList();

        return failures;
    }

    internal static IEnumerable<string> TestSpecificationForCompliance(ImmutableList<Specification> purgeConditions, Specification specification)
    {
        return specification.Matches.SelectMany(match => TestMatchForCompliance(purgeConditions, match));
    }

    private static IEnumerable<string> TestMatchForCompliance(ImmutableList<Specification> purgeConditions, Match match)
    {
        var failedUnknownConditions = purgeConditions
            .Where(pc => pc.Givens[0].Label.Type == match.Unknown.Type &&
                !HasCondition(match.ExistentialConditions, pc))
            .ToList();
        if (failedUnknownConditions.Count > 0)
        {
            var specificationDescriptions = failedUnknownConditions
                .Select(pc => DescribePurgeCondition(pc))
                .ToList();
            return new[] { $"The match for {match.Unknown.Type} is missing purge conditions:\n{string.Join(Environment.NewLine, specificationDescriptions)}" };
        }
        return new string[0];
    }

    private static bool HasCondition(ImmutableList<ExistentialCondition> existentialConditions, Specification purgeCondition)
    {
        return existentialConditions.Any(ec => ConditionMatches(ec, purgeCondition));
    }

    private static bool ConditionMatches(ExistentialCondition condition, Specification purgeCondition)
    {
        if (condition.Exists)
        {
            // We only match negative existential conditions.
            return false;
        }
        // Compare the matches of the condition with the matches of the purge condition.
        if (condition.Matches.Count != purgeCondition.Matches.Count)
        {
            return false;
        }
        return condition.Matches
            .Zip(purgeCondition.Matches, (a, b) => MatchesAreEquivalent(a, b))
            .All(x => x);
    }

    private static bool MatchesAreEquivalent(Match match, Match purgeMatch)
    {
        if (match.Unknown.Type != purgeMatch.Unknown.Type)
        {
            return false;
        }
        if (match.PathConditions.Count != purgeMatch.PathConditions.Count)
        {
            return false;
        }
        if (match.ExistentialConditions.Count != purgeMatch.ExistentialConditions.Count)
        {
            return false;
        }
        return
            match.PathConditions
                .Zip(purgeMatch.PathConditions, (c, pc) => PathConditionsAreEquivalent(c, pc))
                .All(x => x) &&
            match.ExistentialConditions
                .Zip(purgeMatch.ExistentialConditions, (c, pc) => ExistentialConditionsAreEquivalent(c, pc))
                .All(x => x);
    }

    private static bool PathConditionsAreEquivalent(PathCondition condition, PathCondition purgeCondition)
    {
        if (condition.RolesLeft.Count != purgeCondition.RolesLeft.Count)
        {
            return false;
        }
        if (condition.RolesRight.Count != purgeCondition.RolesRight.Count)
        {
            return false;
        }
        return
            condition.RolesLeft
                .Zip(purgeCondition.RolesLeft, (r, pr) => RolesAreEquivalent(r, pr))
                .All(x => x) &&
            condition.RolesRight
                .Zip(purgeCondition.RolesRight, (r, pr) => RolesAreEquivalent(r, pr))
                .All(x => x);
    }

    private static bool ExistentialConditionsAreEquivalent(ExistentialCondition condition, ExistentialCondition purgeCondition)
    {
        if (condition.Exists != purgeCondition.Exists)
        {
            return false;
        }
        if (condition.Matches.Count != purgeCondition.Matches.Count)
        {
            return false;
        }
        return condition.Matches
            .Zip(purgeCondition.Matches, (m, pm) => MatchesAreEquivalent(m, pm))
            .All(x => x);
    }

    private static bool RolesAreEquivalent(Role role, Role purgeRole)
    {
        return role.TargetType == purgeRole.TargetType &&
               role.Name == purgeRole.Name;
    }

    private static string DescribePurgeCondition(Specification purgeCondition)
    {
        return $"!E ({purgeCondition.Givens[0].Label.Name}: {purgeCondition.Givens[0].Label.Type}) {{\n" +
            string.Join("", purgeCondition.Matches.Select(match => match.ToDescriptiveString(1))) +
            "}";
    }

    private static string DescribeTuple(IEnumerable<Match> matches)
    {
        return string.Join(", ", matches.Select(match => match.Unknown.Type));
    }
}
