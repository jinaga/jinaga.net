using System.Linq;
using System.Collections.Generic;
using Jinaga.Projections;
using System.Collections.Immutable;
using System;

namespace Jinaga.Pipelines
{
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
            var initialSubset = specification.Given.Aggregate(
                Subset.Empty,
                (subset, given) => subset.Add(given.Name)
            );

            // The final subset includes all unknowns.
            Subset finalSubset = AddUnknowns(initialSubset, matches);

            var labels = specification.Matches.Select(match => match.Unknown);
            var collectionIdentifiers = ImmutableList<CollectionIdentifier>.Empty;
            var inverses = InvertMatches(matches, initialSubset, finalSubset, labels, collectionIdentifiers, specification.Projection);
            inverses = inverses.AddRange(InvertProjection(matches, initialSubset, finalSubset, labels, collectionIdentifiers, specification.Projection));
            return inverses;
        }

        private static ImmutableList<Inverse> InvertMatches(ImmutableList<Match> matches, Subset initialSubset, Subset finalSubset, IEnumerable<Label> labels, ImmutableList<CollectionIdentifier> collectionIdentifiers, Projection projection)
        {
            // Produce an inverse for each unknown in the original specification.
            var inverses = ImmutableList<Inverse>.Empty;
            foreach (var label in labels)
            {
                matches = ShakeTree(matches, label.Name);
                var inverseSpecification = new Specification(
                    ImmutableList.Create(label),
                    matches.RemoveAt(0),
                    projection
                );
                var inverse = new Inverse(
                    inverseSpecification,
                    initialSubset,
                    Operation.Add,
                    finalSubset,
                    projection,
                    collectionIdentifiers
                );
                inverses = inverses.Add(inverse);

                inverses = inverses.AddRange(InvertExistentialConditions(matches, initialSubset, finalSubset, projection, matches[0].Conditions));
            }

            return inverses;
        }

        private static ImmutableList<Inverse> InvertExistentialConditions(ImmutableList<Match> outerMatches, Subset initialSubset, Subset finalSubset, Projection projection, ImmutableList<MatchCondition> conditions)
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
                            projection
                        );
                        var inverse = new Inverse(
                            inverseSpecification,
                            initialSubset,
                            existentialCondition.Exists ? Operation.Add : Operation.Remove,
                            finalSubset,
                            projection,
                            ImmutableList<CollectionIdentifier>.Empty
                        );
                        inverses = inverses.Add(inverse);
                    }
                }
            }

            return inverses;
        }

        private static ImmutableList<Inverse> InvertProjection(ImmutableList<Match> matches, Subset initialSubset, Subset intermediateSubset, IEnumerable<Label> labels, ImmutableList<CollectionIdentifier> collectionIdentifiers, Projection projection)
        {
            ImmutableList<Inverse> inverses = ImmutableList<Inverse>.Empty;

            // Produce inverses for all collections in the projection.
            foreach (var (name, collection) in CollectionsOf(projection, ""))
            {
                var collectionSubset = AddUnknowns(intermediateSubset, collection.Matches);
                var collectionLabels = collection.Matches.Select(match => match.Unknown);
                var collectionMatches = matches.AddRange(collection.Matches);
                var childCollectionIdentifiers = collectionIdentifiers.Add(new CollectionIdentifier(name, intermediateSubset));
                inverses = inverses.AddRange(InvertMatches(collectionMatches, initialSubset, collectionSubset, collectionLabels, childCollectionIdentifiers, collection.Projection));
                inverses = inverses.AddRange(InvertProjection(collectionMatches, initialSubset, collectionSubset, collectionLabels, childCollectionIdentifiers, collection.Projection));
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
    }

    class InverterOld
    {
        public static IEnumerable<InverseOld> InvertSpecification(SpecificationOld specification)
        {
            var inverses =
                from start in specification.Pipeline.Starts
                from path in specification.Pipeline.Paths
                where path.Start == start
                from inverse in InvertPaths(start, specification.Pipeline, path, specification.Projection, PipelineOld.Empty, ImmutableList<CollectionIdentifier>.Empty)
                select inverse;
            return inverses;
        }

        public static IEnumerable<InverseOld> InvertPipeline(PipelineOld pipeline)
        {
            var inverses =
                from start in pipeline.Starts
                from path in pipeline.Paths
                where path.Start == start
                from inverse in InvertPaths(start, pipeline, path, new EmptyProjection(), PipelineOld.Empty, ImmutableList<CollectionIdentifier>.Empty)
                select inverse;
            return inverses;
        }

        public static IEnumerable<InverseOld> InvertPaths(Label start, PipelineOld pipeline, Path path, Projection projection, PipelineOld backward, ImmutableList<CollectionIdentifier> collectionIdentifiers)
        {
            var nextBackward = backward.PrependPath(ReversePath(path));
            var conditionalInverses =
                from conditional in pipeline.Conditionals
                where conditional.Start == path.Target
                from inverse in InvertConditionals(conditional, nextBackward, start.Name, collectionIdentifiers)
                select inverse;
            var nestedInverses =
                from namedSpecification in projection.GetNamedSpecifications()
                let nestedSpecification = namedSpecification.specification
                from nestedPath in nestedSpecification.Pipeline.Paths
                where nestedPath.Start == path.Target
                let collectionIdentifier = NewCollectionIdentifier(namedSpecification.name, Subset.FromPipeline(nextBackward))
                from nestedInverse in InvertPaths(
                    start,
                    nestedSpecification.Pipeline,
                    nestedPath,
                    nestedSpecification.Projection,
                    nextBackward,
                    collectionIdentifiers.Add(collectionIdentifier))
                select nestedInverse;
            var inversePipeline = nextBackward.AddStart(path.Target);
            var newInverse = new InverseOld(
                inversePipeline,
                Subset.Empty.Add(start.Name),
                Operation.Add,
                Subset.FromPipeline(inversePipeline),
                projection,
                collectionIdentifiers);
            var nextInverses =
                from nextPath in pipeline.Paths
                where nextPath.Start == path.Target
                from nextInverse in InvertPaths(start, pipeline, nextPath, projection, nextBackward, collectionIdentifiers)
                select nextInverse;
            return nextInverses.Concat(conditionalInverses).Concat(nestedInverses).Prepend(newInverse);
        }

        private static CollectionIdentifier NewCollectionIdentifier(string collectionName, Subset intermediateSubset)
        {
            return new CollectionIdentifier(collectionName, intermediateSubset);
        }

        public static IEnumerable<InverseOld> InvertConditionals(Conditional conditional, PipelineOld backward, string affectedTag, ImmutableList<CollectionIdentifier> collectionIdentifiers)
        {
            return InvertPipeline(conditional.ChildPipeline)
                .Select(childInverse => new InverseOld(
                    childInverse.InversePipeline.Compose(backward),
                    Subset.Empty.Add(affectedTag),
                    conditional.Exists ? Operation.Add : Operation.Remove,
                    Subset.FromPipeline(backward),
                    childInverse.Projection,
                    collectionIdentifiers));
        }

        public static Path ReversePath(Path path)
        {
            var reverse = new Path(path.Target, path.Start);
            var type = path.Start.Type;
            foreach (var step in path.PredecessorSteps)
            {
                reverse = reverse.PrependSuccessorStep(new Step(step.Role, type));
                type = step.TargetType;
            }
            foreach (var step in path.SuccessorSteps)
            {
                reverse = reverse.PrependPredecessorStep(new Step(step.Role, type));
                type = step.TargetType;
            }
            return reverse;
        }
    }
}