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
        /// Splits the specification before its first match that the graph cannot run: one that
        /// seeks successors or has an existential condition. That match is the pivot.
        ///
        /// The head runs the matches before the pivot on the graph being authorized. It also
        /// walks, on the tail's behalf, every predecessor path the tail takes from a label in scope
        /// at the pivot: a given, or an unknown of a match before it. Each such walk becomes a head
        /// match binding a split label, and the tail joins to that label instead. The walk may sit
        /// in the pivot, in a later match, or in an existential condition at any depth, but not
        /// beneath a negative existential condition. There the tail would test the facts the walk
        /// reaches one at a time, and a solution that one of them excludes would still be admitted
        /// by another.
        ///
        /// The tail runs on the store, given the labels in scope at the pivot that it uses, and the
        /// head projects them. The tail is null when the graph can run the whole specification.
        ///
        /// This is <c>splitBeforeFirstSuccessor</c> in jinaga-spec (<c>JinagaSpec/Hoist.lean</c>),
        /// where <c>split_correct</c> proves that the head and tail return the specification's
        /// results for every well-formed specification.
        /// </summary>
        public (Specification head, Specification? tail) SplitBeforeFirstSuccessor()
        {
            var specification = Specification;
            var pivotIndex = specification.Matches.FindIndex(match => !IsDeterministic(match));
            if (pivotIndex == -1)
            {
                return (specification, null);
            }

            var before = specification.Matches.GetRange(0, pivotIndex);
            var scope = specification.Givens.Select(given => given.Label.Name)
                .Concat(before.Select(match => match.Unknown.Name))
                .ToImmutableHashSet();
            var hoisted = new List<Match>();
            var tailMatches = HoistMatches(
                specification.Matches.GetRange(pivotIndex, specification.Matches.Count - pivotIndex),
                true, scope, hoisted);
            var headMatches = before.AddRange(hoisted);

            var used = tailMatches.SelectMany(LabelsInMatch)
                .Concat(LabelsInProjection(specification.Projection))
                .ToImmutableHashSet();
            var tailGivens = specification.Givens
                .Concat(headMatches.Select(match =>
                    new SpecificationGiven(match.Unknown, ImmutableList<ExistentialCondition>.Empty)))
                .Where(given => used.Contains(given.Label.Name))
                .ToImmutableList();

            var head = new Specification(
                specification.Givens,
                headMatches,
                new CompoundProjection(
                    tailGivens.ToImmutableDictionary(
                        given => given.Label.Name,
                        given => (Projection)new SimpleProjection(given.Label.Name, typeof(object))),
                    typeof(object)));
            var tail = new Specification(tailGivens, tailMatches, specification.Projection);
            return (head, tail);
        }

        // The graph can run a match that walks only predecessors, along one or more paths.
        private static bool IsDeterministic(Match match) =>
            match.PathConditions.Count > 0 &&
            match.ExistentialConditions.Count == 0 &&
            match.PathConditions.All(condition => condition.RolesLeft.Count == 0);

        // Rewrite matches for the tail, moving each predecessor walk the head can take into
        // `hoisted`. `positive` is false beneath a negative existential condition, and stays false
        // however many conditions are nested inside it.
        private static ImmutableList<Match> HoistMatches(
            ImmutableList<Match> matches, bool positive, ImmutableHashSet<string> scope, List<Match> hoisted)
        {
            return matches
                .Select(match => new Match(
                    match.Unknown,
                    match.PathConditions
                        .Select(condition => HoistPath(condition, positive, scope, hoisted))
                        .ToImmutableList(),
                    match.ExistentialConditions
                        .Select(condition => new ExistentialCondition(
                            condition.Exists,
                            HoistMatches(condition.Matches, positive && condition.Exists, scope, hoisted)))
                        .ToImmutableList()))
                .ToImmutableList();
        }

        private static PathCondition HoistPath(
            PathCondition condition, bool positive, ImmutableHashSet<string> scope, List<Match> hoisted)
        {
            if (!positive || condition.RolesRight.Count == 0 || !scope.Contains(condition.LabelRight))
            {
                return condition;
            }
            var label = new Label(SplitLabel(hoisted.Count), condition.RolesRight.Last().TargetType);
            hoisted.Add(new Match(
                label,
                ImmutableList.Create(new PathCondition(ImmutableList<Role>.Empty, condition.LabelRight, condition.RolesRight)),
                ImmutableList<ExistentialCondition>.Empty));
            return new PathCondition(condition.RolesLeft, label.Name, ImmutableList<Role>.Empty);
        }

        /// <summary>
        /// The label the split gives the fact the head walks to for its <paramref name="index"/>th
        /// walk. It begins with <see cref="ReservedLabelPrefix"/>, so it cannot collide with a
        /// label a well-formed specification declares.
        /// </summary>
        private static string SplitLabel(int index) => $"{ReservedLabelPrefix}s{index}";

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
