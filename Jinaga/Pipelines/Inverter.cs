using System.Linq;
using System.Collections.Generic;
using Jinaga.Projections;
using System.Collections.Immutable;
using System;

namespace Jinaga.Pipelines
{
    class InverterContext
    {
        public InverterContext(Subset givenSubset, ImmutableList<CollectionIdentifier> collectionIdentifiers, Subset resultSubset, Projection projection)
        {
            GivenSubset = givenSubset;
            CollectionIdentifiers = collectionIdentifiers;
            ResultSubset = resultSubset;
            Projection = projection;
        }

        public Subset GivenSubset { get; }
        public ImmutableList<CollectionIdentifier> CollectionIdentifiers { get; }
        public Subset ResultSubset { get; }
        public Projection Projection { get; }
    }
    class Inverter
    {
        public static ImmutableList<Inverse> InvertSpecification(Specification specification)
        {
            // Turn each given into a match.
            var emptyMatches = specification.Given.Select(given =>
                new Match(given, ImmutableList<MatchCondition>.Empty)
            ).ToImmutableList();
            var matches = emptyMatches.AddRange(specification.Matches).ToImmutableList();

            // The initial subset corresponds to the given labels.
            var givenSubset = specification.Given.Aggregate(
                Subset.Empty,
                (subset, given) => subset.Add(given.Name)
            );

            // The final subset includes all unknowns.
            Subset resultSubset = AddUnknowns(givenSubset, matches);

            var labels = specification.Matches.Select(match => match.Unknown);
            var collectionIdentifiers = ImmutableList<CollectionIdentifier>.Empty;
            var context = new InverterContext(
                givenSubset,
                collectionIdentifiers,
                resultSubset,
                specification.Projection);
            var inverses = InvertMatches(matches, labels, context);
            inverses = inverses.AddRange(InvertProjection(matches, context));
            return inverses;
        }

        private static ImmutableList<Inverse> InvertMatches(ImmutableList<Match> matches, IEnumerable<Label> labels, InverterContext context)
        {
            // Produce an inverse for each unknown in the original specification.
            var inverses = ImmutableList<Inverse>.Empty;
            foreach (var label in labels)
            {
                matches = ShakeTree(matches, label.Name);
                // The given will not have any successors.
                // Simplify the matches by removing any conditions that cannot be satisfied.
                var simplifiedMatches = SimplifyMatches(matches, label.Name);
                if (simplifiedMatches != null)
                {
                    var inverseSpecification = new Specification(
                        ImmutableList.Create(label),
                        simplifiedMatches.RemoveAt(0),
                        context.Projection
                    );
                    var inverse = new Inverse(
                        inverseSpecification,
                        context.GivenSubset,
                        InverseOperation.Add,
                        context.ResultSubset,
                        context.CollectionIdentifiers
                    );
                    inverses = inverses.Add(inverse);
                }

                inverses = inverses.AddRange(InvertExistentialConditions(matches, matches[0].Conditions, InverseOperation.Add, context));
            }

            return inverses;
        }

        private static ImmutableList<Inverse> InvertExistentialConditions(ImmutableList<Match> outerMatches, ImmutableList<MatchCondition> conditions, InverseOperation parentOperation, InverterContext context)
        {
            ImmutableList<Inverse> inverses = ImmutableList<Inverse>.Empty;

            // Produce inverses for each existential condition in the match.
            foreach (var condition in conditions)
            {
                if (condition is ExistentialCondition existentialCondition)
                {
                    var matches = outerMatches.AddRange(existentialCondition.Matches);
                    foreach (var match in existentialCondition.Matches)
                    {
                        matches = ShakeTree(matches, match.Unknown.Name);

                        var inverseSpecification = new Specification(
                            ImmutableList.Create(match.Unknown),
                            RemoveCondition(matches.RemoveAt(0), condition),
                            context.Projection
                        );
                        bool exists = existentialCondition.Exists;
                        var operation = InferOperation(parentOperation, exists);
                        var inverse = new Inverse(
                            inverseSpecification,
                            context.GivenSubset,
                            operation,
                            context.ResultSubset,
                            ImmutableList<CollectionIdentifier>.Empty
                        );
                        inverses = inverses.Add(inverse);

                        var existentialInverses = InvertExistentialConditions(matches, match.Conditions, operation, context);
                        inverses = inverses.AddRange(existentialInverses);
                    }
                }
            }

            return inverses;
        }

        private static InverseOperation InferOperation(InverseOperation parentOperation, bool exists)
        {
            if (parentOperation == InverseOperation.Add)
                return exists ? InverseOperation.MaybeAdd : InverseOperation.Remove;
            else if (parentOperation == InverseOperation.Remove || parentOperation == InverseOperation.MaybeRemove)
                return exists ? InverseOperation.MaybeRemove : InverseOperation.MaybeAdd;
            else if (parentOperation == InverseOperation.MaybeAdd)
                return exists ? InverseOperation.MaybeAdd : InverseOperation.MaybeRemove;
            else
                throw new ArgumentException($"Cannot infer operation from {parentOperation}, {(exists ? "exists" : "not exists")}");
        }

        private static ImmutableList<Inverse> InvertProjection(ImmutableList<Match> matches, InverterContext context)
        {
            ImmutableList<Inverse> inverses = ImmutableList<Inverse>.Empty;

            // Produce inverses for all collections in the projection.
            foreach (var (name, collection) in CollectionsOf(context.Projection, ""))
            {
                var collectionSubset = AddUnknowns(context.ResultSubset, collection.Matches);
                var collectionLabels = collection.Matches.Select(match => match.Unknown);
                var collectionMatches = matches.AddRange(collection.Matches);
                var childCollectionIdentifiers = context.CollectionIdentifiers.Add(new CollectionIdentifier(name, context.ResultSubset));
                var childContext = new InverterContext(
                    context.GivenSubset,
                    childCollectionIdentifiers,
                    collectionSubset,
                    collection.Projection);
                inverses = inverses.AddRange(InvertMatches(collectionMatches, collectionLabels, childContext));
                inverses = inverses.AddRange(InvertProjection(collectionMatches, childContext));
            }

            return inverses;
        }

        public static ImmutableList<Match> ShakeTree(ImmutableList<Match> matches, string label)
        {
            // Find the match for the given label.
            var match = FindMatch(matches, label);

            // Move the match to the beginning of the list.
            matches = matches.Remove(match).Insert(0, match);

            // Invert all path conditions in the match and move them to the tagged match.
            foreach (var condition in match.Conditions)
            {
                if (condition is PathCondition pathCondition)
                {
                    matches = InvertAndMovePathCondition(matches, label, pathCondition);
                }
            }

            // Move any other matches with no paths down.
            for (int i = 1; i < matches.Count; i++)
            {
                var otherMatch = matches[i];
                while (otherMatch.Conditions.All(condition => !(condition is PathCondition)))
                {
                    // Find all matches beyond this point that tag this one.
                    for (int j = i + 1; j < matches.Count; j++)
                    {
                        var taggedMatch = matches[j];
                        // Move their path conditions to the other match.
                        var taggedConditions = taggedMatch.Conditions.OfType<PathCondition>()
                            .Where(c => c.LabelRight == otherMatch.Unknown.Name)
                            .ToImmutableList();
                        foreach (var pathCondition in taggedConditions)
                        {
                            matches = InvertAndMovePathCondition(matches, taggedMatch.Unknown.Name, pathCondition);
                        }
                    }
                    // Move the other match to the bottom of the list.
                    otherMatch = matches[i];
                    matches = matches.Remove(otherMatch).Add(otherMatch);
                    otherMatch = matches[i];
                }
            }

            return matches;
        }

        private static ImmutableList<Match> InvertAndMovePathCondition(ImmutableList<Match> matches, string label, PathCondition pathCondition)
        {
            // Find the match for the given label.
            var match = FindMatch(matches, label);

            // Find the match for the target label.
            var taggedMatch = FindMatch(matches, pathCondition.LabelRight);

            // Invert the path condition.
            var invertedPathCondition = new PathCondition(
                pathCondition.RolesRight,
                match.Unknown.Name,
                pathCondition.RolesLeft
            );

            // Remove the path condition from the match.
            var newMatch = new Match(
                match.Unknown,
                match.Conditions.Remove(pathCondition)
            );
            matches = matches.Replace(match, newMatch);

            // Add the inverted path condition to the tagged match.
            var newTaggedMatch = new Match(
                taggedMatch.Unknown,
                taggedMatch.Conditions.Insert(0, invertedPathCondition)
            );
            matches = matches.Replace(taggedMatch, newTaggedMatch);

            return matches;
        }

        private static ImmutableList<Match> RemoveCondition(ImmutableList<Match> matches, MatchCondition condition)
        {
            return matches.Select(match =>
                match.Conditions.Contains(condition)
                    ? new Match(match.Unknown, match.Conditions.Remove(condition))
                    : match
            ).ToImmutableList();
        }

        private static Match FindMatch(ImmutableList<Match> matches, string label)
        {
            var match = matches.FirstOrDefault(m => m.Unknown.Name == label);
            if (match == null)
            {
                throw new ArgumentException($"Malformed specification. Unknown label {label}.");
            }

            return match;
        }

        private static Subset AddUnknowns(Subset initialSubset, ImmutableList<Match> matches)
        {
            return matches.Aggregate(
                initialSubset,
                (subset, match) => subset.Add(match.Unknown.Name)
            );
        }

        private static IEnumerable<(string, CollectionProjection)> CollectionsOf(Projection projection, string name)
        {
            if (projection is CollectionProjection collectionProjection)
            {
                yield return (name, collectionProjection);
            }
            else if (projection is CompoundProjection compoundProjection)
            {
                foreach (var childName in compoundProjection.Names)
                {
                    var childProjection = compoundProjection.GetProjection(childName);
                    foreach (var pair in CollectionsOf(childProjection, childName))
                    {
                        yield return pair;
                    }
                }
            }
        }

        private static ImmutableList<Match>? SimplifyMatches(ImmutableList<Match> matches, string given)
        {
            var simplifiedMatches = ImmutableList<Match>.Empty;

            foreach (var match in matches)
            {
                var simplifiedMatch = SimplifyMatch(match, given);
                if (simplifiedMatch == null)
                {
                    return null;
                }

                simplifiedMatches = simplifiedMatches.Add(simplifiedMatch);
            }

            return simplifiedMatches;
        }

        private static Match? SimplifyMatch(Match match, string given)
        {
            var simplifiedConditions = ImmutableList<MatchCondition>.Empty;

            foreach (var condition in match.Conditions)
            {
                if (ExpectsSuccessor(given, condition))
                {
                    // This path condition matches successors of the given.
                    // There are no successors yet, so the condition is unsatisfiable.
                    return null;
                }

                if (condition is ExistentialCondition existentialCondition)
                {
                    var anyExpectsSuccessor = existentialCondition.Matches.Any(m =>
                        m.Conditions.Any(c =>
                            ExpectsSuccessor(given, c)));
                    if (anyExpectsSuccessor && existentialCondition.Exists)
                    {
                        // This existential condition expects successors of the given.
                        // There are no successors yet, so the condition is unsatisfiable.
                        return null;
                    }
                }

                simplifiedConditions = simplifiedConditions.Add(condition);
            }

            return new Match(match.Unknown, simplifiedConditions);
        }

        private static bool ExpectsSuccessor(string given, MatchCondition condition)
        {
            return condition is PathCondition pathCondition &&
                pathCondition.LabelRight == given &&
                pathCondition.RolesRight.Count == 0 &&
                pathCondition.RolesLeft.Count > 0;
        }
    }
}