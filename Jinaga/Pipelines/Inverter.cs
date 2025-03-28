using System.Linq;
using System.Collections.Generic;
using Jinaga.Projections;
using System.Collections.Immutable;
using System;

namespace Jinaga.Pipelines
{
    class InverterContext
    {
        public InverterContext(Subset givenSubset, ImmutableList<CollectionIdentifier> collectionIdentifiers, Subset resultSubset, Projection projection, Subset parentSubset, string path)
        {
            GivenSubset = givenSubset;
            CollectionIdentifiers = collectionIdentifiers;
            ResultSubset = resultSubset;
            Projection = projection;
            ParentSubset = parentSubset;
            Path = path;
        }

        public Subset GivenSubset { get; }
        public ImmutableList<CollectionIdentifier> CollectionIdentifiers { get; }
        public Subset ResultSubset { get; }
        public Projection Projection { get; }
        public Subset ParentSubset { get; }
        public string Path { get; }
    }
    class Inverter
    {
        public static ImmutableList<Inverse> InvertSpecification(Specification specification)
        {
            // Turn each given into a match.
            var emptyMatches = specification.Givens.Select(g =>
                new Match(
                    g.Label,
                    ImmutableList<PathCondition>.Empty,
                    ImmutableList<ExistentialCondition>.Empty)
            ).ToImmutableList();
            var matches = emptyMatches.AddRange(specification.Matches).ToImmutableList();

            // The initial subset corresponds to the given labels.
            var givenSubset = specification.Givens.Aggregate(
                Subset.Empty,
                (subset, given) => subset.Add(given.Label.Name)
            );

            // The final subset includes all unknowns.
            Subset resultSubset = AddUnknowns(givenSubset, matches);

            var labels = specification.Matches.Select(match => match.Unknown);
            var collectionIdentifiers = ImmutableList<CollectionIdentifier>.Empty;
            var context = new InverterContext(
                givenSubset,
                collectionIdentifiers,
                resultSubset,
                specification.Projection,
                givenSubset,
                "");
            var inverses = InvertMatches(matches, labels, context);
            inverses = inverses.AddRange(InvertProjection(matches, context));
            return inverses;
        }

        private static ImmutableList<Inverse> InvertMatches(ImmutableList<Match> matches, IEnumerable<Label> labels, InverterContext context)
        {
            var originalMatches = matches;

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
                        ImmutableList.Create(
                            new SpecificationGiven(label, simplifiedMatches.First().ExistentialConditions)
                        ),
                        simplifiedMatches.RemoveAt(0),
                        context.Projection
                    );
                    var inverse = new Inverse(
                        inverseSpecification,
                        context.GivenSubset,
                        InverseOperation.Add,
                        context.ResultSubset,
                        context.Path,
                        context.ParentSubset
                    );
                    inverses = inverses.Add(inverse);
                }
            }

            // Produce inverses for each existential condition in every match.
            foreach (var match in originalMatches)
            {
                inverses = inverses.AddRange(InvertExistentialConditions(originalMatches, match.ExistentialConditions, InverseOperation.Add, context));
            }

            return inverses;
        }

        private static ImmutableList<Inverse> InvertExistentialConditions(ImmutableList<Match> outerMatches, ImmutableList<ExistentialCondition> existentialConditions, InverseOperation parentOperation, InverterContext context)
        {
            ImmutableList<Inverse> inverses = ImmutableList<Inverse>.Empty;

            // Produce inverses for each existential condition in the match.
            foreach (var existentialCondition in existentialConditions)
            {
                var initialMatches = outerMatches.AddRange(existentialCondition.Matches);
                var matches = RemoveExistentialCondition(initialMatches, existentialCondition);

                bool exists = existentialCondition.Exists;
                var operation = InferOperation(parentOperation, exists);

                foreach (var match in existentialCondition.Matches)
                {
                    matches = ShakeTree(matches, match.Unknown.Name);

                    var inverseSpecification = new Specification(
                        ImmutableList.Create(
                            new SpecificationGiven(match.Unknown, matches.First().ExistentialConditions)
                        ),
                        matches.RemoveAt(0),
                        context.Projection
                    );
                    var inverse = new Inverse(
                        inverseSpecification,
                        context.GivenSubset,
                        operation,
                        context.ResultSubset,
                        context.Path,
                        context.ParentSubset
                    );
                    inverses = inverses.Add(inverse);
                }

                // Recursively produce inverses for existential conditions in those matches.
                foreach (var match in existentialCondition.Matches)
                {
                    inverses = inverses.AddRange(InvertExistentialConditions(initialMatches, match.ExistentialConditions, operation, context));
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
                string path = string.IsNullOrEmpty(context.Path) ? name : $"{context.Path}.{name}";
                var childContext = new InverterContext(
                    context.GivenSubset,
                    childCollectionIdentifiers,
                    collectionSubset,
                    collection.Projection,
                    context.ResultSubset,
                    path);
                inverses = inverses.AddRange(InvertMatches(collectionMatches, collectionLabels, childContext));
                inverses = inverses.AddRange(InvertProjection(collectionMatches, childContext));
            }

            return inverses;
        }

        private static ImmutableList<Match> ShakeTree(ImmutableList<Match> matches, string label)
        {
            // Break the graph down.
            ImmutableList<Label> unknowns = matches.Select(match => match.Unknown).ToImmutableList();
            ImmutableList<SpecificationEdge> edges = matches.SelectMany(match =>
                match.PathConditions.Select(pathCondition =>
                    new SpecificationEdge(
                        match.Unknown.Name,
                        pathCondition
                    )
                )
            ).ToImmutableList();
            ImmutableList<ExistentialCondition> existentialConditions = matches.SelectMany(match => match.ExistentialConditions).ToImmutableList();

            // Build the graph via depth-first search.
            ImmutableList<Match> newMatches = ImmutableList<Match>.Empty;
            newMatches = Visit(label, newMatches, ref unknowns, ref edges, ref existentialConditions);

            // Verify that all of the unknowns, edges, and existential conditions have been consumed.
            if (unknowns.Count > 0 || edges.Count > 0 || existentialConditions.Count > 0)
            {
                throw new ArgumentException("Malformed specification.");
            }

            return newMatches;
        }

        private static ImmutableList<Match> Visit(string label, ImmutableList<Match> newMatches, ref ImmutableList<Label> unknowns, ref ImmutableList<SpecificationEdge> edges, ref ImmutableList<ExistentialCondition> existentialConditions)
        {
            // Find the unknown by label.
            var unknown = unknowns.SingleOrDefault(u => u.Name == label);
            if (unknown is null)
            {
                return newMatches;
            }

            // Find the edges that connect with this unknown.
            var connectedEdges = edges.Where(e => e.ConnectsWith(label)).ToImmutableList();

            // Find those edges that also connect with unknowns already in the new matches.
            var doublyConnectedEdges = connectedEdges.Where(e =>
                newMatches.Any(m => e.ConnectsWith(m.Unknown.Name))
            ).ToImmutableList();
            var singlyConnectedEdges = connectedEdges.Except(doublyConnectedEdges).ToImmutableList();

            // Invert the doubly connected edges if necessary.
            var newPathConditions = doublyConnectedEdges.Select(e => e.WithLeft(label)).ToImmutableList();

            // Find all existential conditions whose connections are completely satisfied.
            var unknownLabels = newMatches.Select(m => m.Unknown.Name).ToImmutableHashSet().Add(label);
            var satisfiedExistentialConditions = existentialConditions.Where(ec =>
                IsSatisfied(unknownLabels, ec.Matches)
            ).ToImmutableList();

            // Add the unknown to the new matches.
            var newMatch = new Match(
                unknown,
                newPathConditions,
                satisfiedExistentialConditions
            );
            newMatches = newMatches.Add(newMatch);

            // Consume the source collections.
            unknowns = unknowns.Remove(unknown);
            edges = edges.Except(doublyConnectedEdges).ToImmutableList();
            existentialConditions = existentialConditions.Except(satisfiedExistentialConditions).ToImmutableList();

            // Visit the singly connected edges.
            foreach (var edge in singlyConnectedEdges)
            {
                newMatches = Visit(edge.OtherLabel(label), newMatches, ref unknowns, ref edges, ref existentialConditions);
            }

            return newMatches;
        }

        private static bool IsSatisfied(ImmutableHashSet<string> unknownLabels, ImmutableList<Match> matches)
        {
            foreach (var match in matches)
            {
                unknownLabels = unknownLabels.Add(match.Unknown.Name);
                bool everyPathConditionIsSatisfied = match.PathConditions.All(pc =>
                    unknownLabels.Contains(pc.LabelRight)
                );
                if (!everyPathConditionIsSatisfied)
                {
                    return false;
                }

                bool everyExistentialConditionIsSatisfied = match.ExistentialConditions.All(ec =>
                    IsSatisfied(unknownLabels, ec.Matches)
                );
                if (!everyExistentialConditionIsSatisfied)
                {
                    return false;
                }
            }

            return true;
        }

        private static ImmutableList<Match> RemoveExistentialCondition(ImmutableList<Match> matches, ExistentialCondition existentialCondition)
        {
            return matches.Select(match =>
                match.ExistentialConditions.Contains(existentialCondition)
                    ? new Match(match.Unknown, match.PathConditions, match.ExistentialConditions.Remove(existentialCondition))
                    : match
            ).ToImmutableList();
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
            var simplifiedPathConditions = ImmutableList<PathCondition>.Empty;
            var simplifiedExistentialConditions = ImmutableList<ExistentialCondition>.Empty;

            foreach (var pathCondition in match.PathConditions)
            {
                if (ExpectsSuccessor(given, pathCondition))
                {
                    // This path condition matches successors of the given.
                    // There are no successors yet, so the condition is unsatisfiable.
                    return null;
                }

                simplifiedPathConditions = simplifiedPathConditions.Add(pathCondition);
            }
            foreach (var existentialCondition in match.ExistentialConditions)
            {
                var anyExpectsSuccessor = existentialCondition.Matches.Any(m =>
                    m.PathConditions.Any(c =>
                        ExpectsSuccessor(given, c)));
                if (anyExpectsSuccessor && existentialCondition.Exists)
                {
                    // This existential condition expects successors of the given.
                    // There are no successors yet, so the condition is unsatisfiable.
                    return null;
                }

                simplifiedExistentialConditions = simplifiedExistentialConditions.Add(existentialCondition);
            }

            return new Match(match.Unknown, simplifiedPathConditions, simplifiedExistentialConditions);
        }

        private static bool ExpectsSuccessor(string given, PathCondition pathCondition)
        {
            return
                pathCondition.LabelRight == given &&
                pathCondition.RolesRight.Count == 0 &&
                pathCondition.RolesLeft.Count > 0;
        }
    }
}