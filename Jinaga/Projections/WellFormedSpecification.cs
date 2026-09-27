using Jinaga.Pipelines;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    /// <summary>
    /// A specification that <see cref="Check"/> accepted. Every label a path condition names is in
    /// scope, no match declares a label that is already in scope or one reserved for the split,
    /// and the projection names only labels the specification declares.
    ///
    /// These are the preconditions of the split, stated and proved in jinaga-spec
    /// (<c>JinagaSpec/WellFormed.lean</c>, <c>docs/contracts.md</c>). Only <see cref="Check"/> makes
    /// one, so the split, which relies on them, takes this type and does not check them again.
    /// </summary>
    internal sealed class WellFormedSpecification
    {
        /// <summary>
        /// Labels that begin with this prefix belong to the split, which names the facts it hands
        /// from the head to the tail. A specification may not declare one.
        /// </summary>
        public const string ReservedLabelPrefix = "__";

        private WellFormedSpecification(Specification specification)
        {
            Specification = specification;
        }

        public Specification Specification { get; }

        /// <summary>
        /// Check a specification where it enters, and vouch for it.
        /// </summary>
        /// <param name="specification">The specification to check.</param>
        /// <param name="description">Names the specification in the error message.</param>
        /// <exception cref="InvalidOperationException">The specification is not well formed.</exception>
        public static WellFormedSpecification Check(Specification specification, string description)
        {
            var errors = Errors(specification);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException($"{description} is not valid. {string.Join(" ", errors)}");
            }
            return new WellFormedSpecification(specification);
        }

        /// <summary>
        /// Report where a specification names a label that is not in scope, declares a label that
        /// already is, declares one reserved for the split, or projects a label it does not declare.
        /// </summary>
        /// <returns>One message per defect, or an empty list if the specification is well formed.</returns>
        public static ImmutableList<string> Errors(Specification specification)
        {
            var errors = ImmutableList.CreateBuilder<string>();
            var givens = specification.Givens.Select(given => given.Label.Name).ToImmutableList();
            foreach (var given in givens)
            {
                CheckNotReserved(given, errors);
            }
            CheckScope(givens, specification.Matches, errors);
            var declared = givens.AddRange(specification.Matches.Select(match => match.Unknown.Name));
            foreach (var label in ProjectedLabels(specification.Projection))
            {
                if (!declared.Contains(label))
                {
                    errors.Add($"The projection names the label '{label}', which has not been defined.");
                }
            }
            return errors.ToImmutable();
        }

        // A match sees the givens, the matches before it, and, inside its own existential
        // conditions, its own unknown. It never joins its own unknown. Scope is lexical, so
        // sibling existential conditions may reuse a name.
        private static void CheckScope(ImmutableList<string> scope, ImmutableList<Match> matches, ImmutableList<string>.Builder errors)
        {
            foreach (var match in matches)
            {
                var name = match.Unknown.Name;
                if (scope.Contains(name))
                {
                    errors.Add($"The name '{name}' has already been used.");
                }
                CheckNotReserved(name, errors);
                foreach (var condition in match.PathConditions)
                {
                    if (!scope.Contains(condition.LabelRight))
                    {
                        errors.Add($"The label '{condition.LabelRight}' has not been defined.");
                    }
                }
                var inner = scope.Add(name);
                foreach (var condition in match.ExistentialConditions)
                {
                    CheckScope(inner, condition.Matches, errors);
                }
                scope = inner;
            }
        }

        private static void CheckNotReserved(string name, ImmutableList<string>.Builder errors)
        {
            if (name.StartsWith(ReservedLabelPrefix, StringComparison.Ordinal))
            {
                errors.Add($"The name '{name}' is reserved: labels that begin with '{ReservedLabelPrefix}' belong to the split.");
            }
        }

        // The labels a projection names directly. A collection declares its own labels, which
        // the check does not model, so it contributes none.
        private static IEnumerable<string> ProjectedLabels(Projection projection)
        {
            switch (projection)
            {
                case SimpleProjection simple:
                    return new[] { simple.Tag };
                case FieldProjection field:
                    return new[] { field.Tag };
                case HashProjection hash:
                    return new[] { hash.Tag };
                case CompoundProjection compound:
                    return compound.Names.SelectMany(name => ProjectedLabels(compound.GetProjection(name)));
                default:
                    return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// Splits the specification before the first match that seeks successors.
        ///
        /// The head contains only predecessor joins, so it can run on a fact graph that has not
        /// been saved. The tail runs on the store, given the labels of the head that it needs.
        /// Either may be null: no head means the specification starts with a successor join, and
        /// no tail means the whole specification is deterministic.
        /// </summary>
        public (Specification? head, Specification? tail) SplitBeforeFirstSuccessor()
        {
            var specification = Specification;

            // A match is deterministic when every one of its path conditions walks only
            // predecessors. Several such conditions intersect, which the graph can still run.
            var pivotIndex = specification.Matches.FindIndex(match =>
                match.PathConditions.Count == 0 ||
                match.ExistentialConditions.Count != 0 ||
                match.PathConditions.Any(condition => condition.RolesLeft.Count != 0));

            if (pivotIndex == -1)
            {
                // No match seeks successors, so the whole specification is deterministic.
                return (specification, null);
            }

            var pivot = specification.Matches[pivotIndex];
            if (pivot.PathConditions.Count != 1)
            {
                return (null, specification);
            }

            var condition = pivot.PathConditions[0];
            var unknownsAsGivens = specification.Matches
                .Select(match => new SpecificationGiven(match.Unknown, ImmutableList<ExistentialCondition>.Empty));

            if (condition.RolesRight.Count == 0)
            {
                // The path contains only successor joins. Put the entire match in the tail.
                if (pivotIndex == 0)
                {
                    return (null, specification);
                }

                var headMatches = specification.Matches.GetRange(0, pivotIndex);
                var tailMatches = specification.Matches.GetRange(pivotIndex, specification.Matches.Count - pivotIndex);
                var head = new Specification(
                    ReferencedLabels(headMatches, CompoundProjection.Empty, specification.Givens),
                    headMatches,
                    CompoundProjection.Empty);
                var tail = new Specification(
                    ReferencedLabels(tailMatches, specification.Projection, specification.Givens.AddRange(unknownsAsGivens)),
                    tailMatches,
                    specification.Projection);
                return (head, tail);
            }
            else
            {
                // The path contains both predecessor and successor joins. Split it at a new label.
                var usedNames = specification.Givens.Select(g => g.Label.Name)
                    .Concat(specification.Matches.Select(m => m.Unknown.Name))
                    .ToImmutableHashSet();
                var splitName = Enumerable.Range(1, int.MaxValue)
                    .Select(i => $"s{i}")
                    .First(name => !usedNames.Contains(name));
                var splitLabel = new Label(splitName, condition.RolesRight.Last().TargetType);

                var headMatch = new Match(
                    splitLabel,
                    ImmutableList.Create(new PathCondition(
                        ImmutableList<Role>.Empty, condition.LabelRight, condition.RolesRight)),
                    ImmutableList<ExistentialCondition>.Empty);
                var tailMatch = new Match(
                    pivot.Unknown,
                    ImmutableList.Create(new PathCondition(
                        condition.RolesLeft, splitLabel.Name, ImmutableList<Role>.Empty)),
                    pivot.ExistentialConditions);

                var headMatches = specification.Matches.GetRange(0, pivotIndex).Add(headMatch);
                var tailMatches = specification.Matches.GetRange(pivotIndex + 1, specification.Matches.Count - pivotIndex - 1)
                    .Insert(0, tailMatch);
                var allLabels = specification.Givens
                    .AddRange(unknownsAsGivens)
                    .Add(new SpecificationGiven(splitLabel, ImmutableList<ExistentialCondition>.Empty));
                var head = new Specification(
                    ReferencedLabels(headMatches, CompoundProjection.Empty, specification.Givens),
                    headMatches,
                    CompoundProjection.Empty);
                var tail = new Specification(
                    ReferencedLabels(tailMatches, specification.Projection, allLabels),
                    tailMatches,
                    specification.Projection);
                return (head, tail);
            }
        }

        private static ImmutableList<SpecificationGiven> ReferencedLabels(ImmutableList<Match> matches, Projection projection, ImmutableList<SpecificationGiven> labels)
        {
            // A label the projection uses has to be carried in even when no match mentions it,
            // or a tail projecting a label bound in the head would have nothing to project.
            var definedLabels = matches.Select(match => match.Unknown.Name).ToImmutableHashSet();
            var referencedLabels = matches
                .SelectMany(LabelsInMatch)
                .Concat(LabelsInProjection(projection))
                .Where(label => !definedLabels.Contains(label))
                .ToImmutableHashSet();
            return labels
                .Where(given => referencedLabels.Contains(given.Label.Name))
                .ToImmutableList();
        }

        private static IEnumerable<string> LabelsInMatch(Match match)
        {
            return match.PathConditions
                .Select(condition => condition.LabelRight)
                .Concat(match.ExistentialConditions
                    .SelectMany(condition => condition.Matches)
                    .SelectMany(LabelsInMatch));
        }

        private static IEnumerable<string> LabelsInProjection(Projection projection)
        {
            switch (projection)
            {
                case SimpleProjection simple:
                    return new[] { simple.Tag };
                case FieldProjection field:
                    return new[] { field.Tag };
                case HashProjection hash:
                    return new[] { hash.Tag };
                case CompoundProjection compound:
                    return compound.Names
                        .SelectMany(name => LabelsInProjection(compound.GetProjection(name)));
                case CollectionProjection collection:
                    var defined = collection.Matches.Select(match => match.Unknown.Name).ToImmutableHashSet();
                    return collection.Matches
                        .SelectMany(LabelsInMatch)
                        .Concat(LabelsInProjection(collection.Projection))
                        .Where(label => !defined.Contains(label));
                default:
                    return Enumerable.Empty<string>();
            }
        }
    }
}
