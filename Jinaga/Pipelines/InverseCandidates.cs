using Jinaga.Projections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines
{
    /// <summary>
    /// jinaga-spec's proven inverse algorithm, <c>candidates</c>, ported from
    /// <c>ports/typescript/inverse-candidates.ts</c> at jinaga-spec
    /// <c>65a5d18524a05e7720f3b58e5a9b59d9e1b523bf</c>, together with the exact
    /// inverses of <c>JinagaSpec/Exact.lean</c> at the same commit
    /// (<see cref="ExactInverses"/>).
    ///
    /// Proven (jinaga/jinaga-spec#7, #11, #12): for a well-formed specification and a
    /// fresh batch -- no stored fact has the hash of a batch fact or names one as a
    /// predecessor, though the batch's facts may name each other -- every row whose
    /// membership changes when the batch is saved is found by one of
    /// <see cref="CandidateInverses"/>, read with the subscription's givens and one of
    /// the batch's facts as <see cref="PinLabel"/>, on the store that holds the whole
    /// batch.
    ///
    /// A <see cref="CandidateInverses"/> inverse carries no operation: every row it
    /// reports is a candidate, and a caller decides each against the store. An
    /// <see cref="ExactInverses"/> inverse carries its direction, so a caller applies
    /// what it reports and reads nothing again.
    ///
    /// These inverses are internal: <see cref="PinLabel"/> is a reserved label, so they
    /// never pass through <see cref="WellFormedSpecification"/>.
    /// </summary>
    /// <remarks>
    /// The reference implementation keeps one interleaved list of conditions per match,
    /// where <see cref="Match"/> keeps path and existential conditions in separate
    /// lists. The algorithm's output depends only on each kind's own order -- a path
    /// condition is never touched and never relaxed away, and an existential condition
    /// is never reordered against a path condition -- so it ports over the two lists.
    /// </remarks>
    internal static class InverseCandidates
    {
        /// <summary>
        /// The label an inverse gives the new fact. Reserved, so no specification
        /// declares it.
        /// </summary>
        public const string PinLabel = "__f";

        private static readonly PathCondition PinCondition = new PathCondition(
            ImmutableList<Role>.Empty,
            PinLabel,
            ImmutableList<Role>.Empty);

        /// <summary>
        /// Step 1: every inverse, read with the subscription's givens and the new fact
        /// as <see cref="PinLabel"/>. One inverse for each way the new fact can touch a
        /// solution. Nothing is deduplicated, and no inverse carries an operation.
        /// </summary>
        public static ImmutableList<Specification> BuildInverses(Specification specification)
        {
            return InversesFor(true, specification)
                .AddRange(InversesFor(false, specification));
        }

        /// <summary>
        /// The inverses in one direction, <c>inversesFor</c> in
        /// <c>JinagaSpec/Candidates.lean</c>. <see cref="BuildInverses"/> is the gained
        /// direction followed by the lost one, and the exact inverses strengthen each
        /// direction on its own.
        /// </summary>
        private static ImmutableList<Specification> InversesFor(bool gained, Specification specification)
        {
            return TouchMatches(gained, specification.Matches)
                .Select(touch => new Specification(
                    specification.Givens.Add(new SpecificationGiven(
                        new Label(PinLabel, touch.Pinned.Type),
                        ImmutableList<ExistentialCondition>.Empty)),
                    touch.Value,
                    specification.Projection))
                .ToImmutableList();
        }

        /// <summary>
        /// Step 2: the inverse reordered to start at the new fact if the result checks,
        /// and otherwise the inverse itself.
        /// </summary>
        public static Specification ReorderInverse(Specification inverse)
        {
            var givens = inverse.Givens
                .Select(given => given.Label.Name)
                .Where(name => name != PinLabel)
                .ToImmutableList();
            var scope = givens.Insert(0, PinLabel);
            var flat = FlattenMatches(inverse.Matches);
            var ordered = OrderFromPin(givens, flat);
            bool ok = ordered != null &&
                IsScoped(scope, inverse.Matches) &&
                Arranges(scope, flat, ordered) &&
                ProjectionLabels(inverse.Projection).All(label =>
                    scope.Contains(label) ||
                    inverse.Matches.Any(match => match.Unknown.Name == label));
            return ok
                ? new Specification(inverse.Givens, ordered!, inverse.Projection)
                : inverse;
        }

        /// <summary>
        /// The whole algorithm's inverses. Read each with the subscription's givens
        /// followed by the new fact; every resulting row is a candidate.
        /// </summary>
        public static ImmutableList<Specification> CandidateInverses(Specification specification)
        {
            return BuildInverses(ReduceGivenConditions(specification))
                .Select(ReorderInverse)
                .ToImmutableList();
        }

        /* ------------- The exact inverses ------------- */

        /// <summary>
        /// The inverses strengthened so that each reports a change in one direction, with
        /// no second read. Ported from <c>addInverses</c>, <c>removeInverses</c> and
        /// <c>changes</c> in <c>JinagaSpec/Exact.lean</c> at jinaga-spec
        /// <c>65a5d18524a05e7720f3b58e5a9b59d9e1b523bf</c>.
        ///
        /// Read each with the subscription's givens and the new fact as
        /// <see cref="PinLabel"/>, on the store that holds the whole batch. Proven in
        /// <c>JinagaSpec/Proofs/Exact.lean</c>: every row an add inverse finds is in the
        /// result on the new graph (<c>adds_sound</c>); no row a remove inverse finds is
        /// (<c>removes_sound</c>); together they report every row whose membership changed
        /// (<c>changes_complete</c>); and so a stream that applies them holds exactly the
        /// result (<c>exact_correct</c>, <c>exact_stream_correct</c>).
        ///
        /// The remove direction is exact under <c>projectsUnknowns</c>: the projection
        /// names every top-level unknown, so a row determines its solution and a test on
        /// that solution decides the row. A caller that projects fewer labels than that
        /// may find a row in <c>Removes</c> that another solution still supports.
        ///
        /// Neither direction looks at the old graph. Adding a row the reader already holds,
        /// or removing one it never held, changes nothing, so a test that is sound on the
        /// new graph and complete is exact.
        /// </summary>
        public static (ImmutableList<Specification> Adds, ImmutableList<Specification> Removes) ExactInverses(Specification specification)
        {
            var reduced = ReduceGivenConditions(specification);
            return (
                AddInverses(reduced).Select(ReorderInverse).ToImmutableList(),
                RemoveInverses(reduced).Select(ReorderInverse).ToImmutableList());
        }

        /// <summary>
        /// The gained-direction inverses, each with every original top-level condition
        /// re-attached. Every solution one finds is then a solution of the specification
        /// on the new graph.
        /// </summary>
        private static ImmutableList<Specification> AddInverses(Specification specification)
        {
            return InversesFor(true, specification)
                .Select(inverse => new Specification(
                    inverse.Givens,
                    Conj(inverse.Matches, specification.Matches),
                    inverse.Projection))
                .ToImmutableList();
        }

        /// <summary>
        /// The lost-direction inverses, each strengthened by one remove test. Every
        /// solution one finds fails a top-level existential condition on the new graph, so
        /// it is not a solution of the specification there. A specification with no
        /// top-level existential condition has none: a row can be lost only when one of
        /// them flips.
        /// </summary>
        private static ImmutableList<Specification> RemoveInverses(Specification specification)
        {
            var tests = RemoveTests(specification.Matches);
            return InversesFor(false, specification)
                .SelectMany(inverse => tests.Select(test => new Specification(
                    inverse.Givens,
                    Conj(inverse.Matches, test),
                    inverse.Projection)))
                .ToImmutableList();
        }

        /// <summary>
        /// For each top-level match and each existential condition on it, the same
        /// unknowns carrying no condition anywhere but that one, flipped, on its own match.
        /// </summary>
        private static ImmutableList<ImmutableList<Match>> RemoveTests(ImmutableList<Match> matches)
        {
            if (matches.IsEmpty)
            {
                return ImmutableList<ImmutableList<Match>>.Empty;
            }
            var match = matches[0];
            var rest = matches.RemoveAt(0);
            var bare = new Match(match.Unknown, ImmutableList<PathCondition>.Empty, ImmutableList<ExistentialCondition>.Empty);
            var here = match.ExistentialConditions
                .Select(condition => rest
                    .Select(later => new Match(
                        later.Unknown,
                        ImmutableList<PathCondition>.Empty,
                        ImmutableList<ExistentialCondition>.Empty))
                    .ToImmutableList()
                    .Insert(0, new Match(
                        match.Unknown,
                        ImmutableList<PathCondition>.Empty,
                        ImmutableList.Create(Flip(condition)))));
            var later = RemoveTests(rest).Select(test => test.Insert(0, bare));
            return here.Concat(later).ToImmutableList();
        }

        /// <summary>The existential condition that asks the opposite.</summary>
        private static ExistentialCondition Flip(ExistentialCondition condition)
        {
            return new ExistentialCondition(!condition.Exists, condition.Matches);
        }

        /// <summary>
        /// Two match lists over the same unknowns, as one: match by match, the unknown and
        /// the conditions of both, dropping from the right every condition structurally
        /// equal to one the left already carries. Every inverse keeps the original's
        /// top-level unknowns in order, so the zip is well defined.
        ///
        /// Lean's <c>conj</c> keeps the duplicates, because a conjunction is idempotent and
        /// nothing it proves can see them. A store can: jinaga/jinaga.net#329 reports SQLite
        /// answering the add inverses of two of the exact-inverse cases with no rows, where
        /// <see cref="Jinaga.Storage.MemoryStore"/> answers one, until the duplicates come
        /// out. <c>Match.ToDescriptiveString</c> collapses them with <c>Union</c>, so only a
        /// comparison of the condition lists themselves can see them.
        /// </summary>
        private static ImmutableList<Match> Conj(ImmutableList<Match> left, ImmutableList<Match> right)
        {
            return left.Zip(right, (l, r) => new Match(
                    l.Unknown,
                    l.PathConditions.AddRange(r.PathConditions
                        .Where(condition => !l.PathConditions.Any(other => SamePath(condition, other)))),
                    l.ExistentialConditions.AddRange(r.ExistentialConditions
                        .Where(condition => !l.ExistentialConditions.Any(other => SameExistential(condition, other))))))
                .ToImmutableList();
        }

        /// <summary>
        /// Move every condition on a given onto a new first match, bound to the first given
        /// that carries one. An existential condition does not read the match it is written
        /// on, so one match serves every given's conditions: it has one solution when all of
        /// them hold, and none when any of them fails. The match takes the first of
        /// <c>given</c>, <c>given2</c>, <c>given3</c>, ... that the specification does not
        /// name, as <c>givenConditionsLabel</c> in <c>JinagaSpec/GivenConditions.lean</c>
        /// does.
        ///
        /// Proven in <c>JinagaSpec/Proofs/GivenConditions.lean</c>:
        /// <c>read_reduceGivenConditions</c>, that the reduction reads exactly what the
        /// conditioned specification reads, for any unused label;
        /// <c>candidates_complete_given</c>, that its candidates cover every change; and
        /// <c>stream_correct_given</c>, that a stream which re-checks them holds exactly the
        /// result.
        ///
        /// The label also avoids every label a collection projection declares or names. The
        /// Lean model has no nested specification projections, so neither it nor the
        /// TypeScript port can see those labels (jinaga/jinaga-spec#18); jinaga.js takes the
        /// same wider rule (jinaga/jinaga.js#371). Avoiding them is conservative -- it can
        /// only skip a name the vectors would have allowed, never pick one they reject -- and
        /// it matters here because <see cref="Jinaga.Observers.ObserverLocal"/> appends a
        /// collection's matches to its parent's, where a collision would put two declarations
        /// of one label in one match list.
        /// </summary>
        public static Specification ReduceGivenConditions(Specification specification)
        {
            var first = specification.Givens
                .FirstOrDefault(given => !given.ExistentialConditions.IsEmpty);
            if (first == null)
            {
                return specification;
            }

            var conditions = specification.Givens
                .SelectMany(given => given.ExistentialConditions)
                .ToImmutableList();
            var used = specification.Givens
                .Select(given => given.Label.Name)
                .Concat(LabelsInMatches(conditions
                    .SelectMany(condition => condition.Matches)
                    .ToImmutableList()))
                .Concat(LabelsInMatches(specification.Matches))
                .Concat(LabelsInProjection(specification.Projection))
                .ToImmutableHashSet();
            string label = "given";
            for (int index = 2; used.Contains(label); index++)
            {
                label = $"given{index}";
            }

            return new Specification(
                specification.Givens
                    .Select(given => new SpecificationGiven(
                        given.Label,
                        ImmutableList<ExistentialCondition>.Empty))
                    .ToImmutableList(),
                specification.Matches.Insert(0, new Match(
                    new Label(label, first.Label.Type),
                    ImmutableList.Create(new PathCondition(
                        ImmutableList<Role>.Empty,
                        first.Label.Name,
                        ImmutableList<Role>.Empty)),
                    conditions)),
                specification.Projection);
        }

        /// <summary>Every label a list of matches declares or names, at any depth.</summary>
        private static IEnumerable<string> LabelsInMatches(ImmutableList<Match> matches)
        {
            return matches.SelectMany(match => ImmutableList.Create(match.Unknown.Name)
                .Concat(match.PathConditions.Select(condition => condition.LabelRight))
                .Concat(match.ExistentialConditions.SelectMany(condition =>
                    LabelsInMatches(condition.Matches))));
        }

        /// <summary>
        /// Every label the projection names, together with every label declared or named, at
        /// any depth, inside a collection projection's matches and its own projection.
        /// </summary>
        private static IEnumerable<string> LabelsInProjection(Projection projection)
        {
            switch (projection)
            {
                case CollectionProjection collection:
                    return LabelsInMatches(collection.Matches)
                        .Concat(LabelsInProjection(collection.Projection));
                case CompoundProjection compound:
                    return compound.Names.SelectMany(name =>
                        LabelsInProjection(compound.GetProjection(name)));
                default:
                    return ProjectionLabels(projection);
            }
        }

        /* ------------- Step 1: one inverse per way the new fact can touch a row ------------- */

        /// <summary>One way the new fact can change a solution, and the label it pins.</summary>
        private class Touch<T>
        {
            public Touch(Label pinned, T value)
            {
                Pinned = pinned;
                Value = value;
            }

            public Label Pinned { get; }
            public T Value { get; }
        }

        /// <summary>
        /// A level that lost its solution keeps only its path conditions. The reference
        /// implementation writes this over one interleaved list of conditions, which here
        /// means dropping the existential conditions and leaving the path conditions as
        /// they are.
        /// </summary>
        private static ImmutableList<ExistentialCondition> Relax(bool gained, ImmutableList<ExistentialCondition> existentialConditions)
        {
            return gained ? existentialConditions : ImmutableList<ExistentialCondition>.Empty;
        }

        private static Match Relax(bool gained, Match match)
        {
            return new Match(match.Unknown, match.PathConditions, Relax(gained, match.ExistentialConditions));
        }

        private static ImmutableList<Match> RelaxMatches(bool gained, ImmutableList<Match> matches)
        {
            return gained
                ? matches
                : matches.Select(match => Relax(false, match)).ToImmutableList();
        }

        /// <summary>
        /// Every way the new fact can change a solution of <paramref name="matches"/>, in
        /// direction <paramref name="gained"/>.
        /// </summary>
        private static IEnumerable<Touch<ImmutableList<Match>>> TouchMatches(bool gained, ImmutableList<Match> matches)
        {
            if (matches.IsEmpty)
            {
                yield break;
            }
            var match = matches[0];
            var rest = matches.RemoveAt(0);

            // Pin this unknown. Only a gained solution can contain the new fact.
            if (gained)
            {
                yield return new Touch<ImmutableList<Match>>(
                    match.Unknown,
                    rest.Insert(0, new Match(
                        match.Unknown,
                        match.PathConditions.Insert(0, PinCondition),
                        match.ExistentialConditions)));
            }

            // Through one of this match's conditions; the matches after it relax.
            foreach (var touch in TouchConditions(gained, match))
            {
                yield return new Touch<ImmutableList<Match>>(
                    touch.Pinned,
                    RelaxMatches(gained, rest).Insert(0, touch.Value));
            }

            // In the matches after it; this match relaxes.
            foreach (var touch in TouchMatches(gained, rest))
            {
                yield return new Touch<ImmutableList<Match>>(
                    touch.Pinned,
                    touch.Value.Insert(0, Relax(gained, match)));
            }
        }

        /// <summary>
        /// Through exactly one of the match's conditions; the others relax. A path
        /// condition is never a way in, so only the existential conditions are touched,
        /// and the path conditions carry over untouched either way.
        /// </summary>
        private static IEnumerable<Touch<Match>> TouchConditions(bool gained, Match match)
        {
            for (int index = 0; index < match.ExistentialConditions.Count; index++)
            {
                var before = match.ExistentialConditions.Take(index).ToImmutableList();
                var after = match.ExistentialConditions.Skip(index + 1).ToImmutableList();
                foreach (var touch in TouchCondition(gained, match.ExistentialConditions[index]))
                {
                    var existentialConditions = Relax(gained, before)
                        .Add(touch.Value)
                        .AddRange(Relax(gained, after));
                    yield return new Touch<Match>(
                        touch.Pinned,
                        new Match(match.Unknown, match.PathConditions, existentialConditions));
                }
            }
        }

        /// <summary>
        /// Into an existential condition, which becomes <c>exists: true</c>. Inside a
        /// notExists, a gain is a loss.
        /// </summary>
        private static IEnumerable<Touch<ExistentialCondition>> TouchCondition(bool gained, ExistentialCondition condition)
        {
            return TouchMatches(gained == condition.Exists, condition.Matches)
                .Select(touch => new Touch<ExistentialCondition>(
                    touch.Pinned,
                    new ExistentialCondition(true, touch.Value)));
        }

        /* ------------- Step 2: start each inverse at the new fact, and check it ------------- */

        private static bool IsPin(PathCondition condition)
        {
            return condition.RolesLeft.IsEmpty &&
                condition.LabelRight == PinLabel &&
                condition.RolesRight.IsEmpty;
        }

        private static bool PinnedMatches(ImmutableList<Match> matches)
        {
            return matches.Any(match =>
                match.PathConditions.Any(IsPin) ||
                match.ExistentialConditions.Any(condition => PinnedMatches(condition.Matches)));
        }

        /// <summary>
        /// Lift the matches of every <c>exists</c> condition that holds the pin, right
        /// after the match that held it.
        /// </summary>
        private static ImmutableList<Match> FlattenMatches(ImmutableList<Match> matches)
        {
            var flattened = ImmutableList<Match>.Empty;
            foreach (var match in matches)
            {
                var keep = ImmutableList<ExistentialCondition>.Empty;
                var lifted = ImmutableList<Match>.Empty;
                foreach (var condition in match.ExistentialConditions)
                {
                    if (condition.Exists && PinnedMatches(condition.Matches))
                    {
                        lifted = lifted.AddRange(FlattenMatches(condition.Matches));
                    }
                    else
                    {
                        keep = keep.Add(condition);
                    }
                }
                flattened = flattened
                    .Add(new Match(match.Unknown, match.PathConditions, keep))
                    .AddRange(lifted);
            }
            return flattened;
        }

        /// <summary>A path condition and the match it is written on.</summary>
        private class Edge
        {
            public Edge(string home, PathCondition condition)
            {
                Home = home;
                Condition = condition;
            }

            public string Home { get; }
            public PathCondition Condition { get; }
        }

        /// <summary>
        /// The unknown a walk reaches next, the edge that reached it, and that edge
        /// written with the unknown on its left.
        /// </summary>
        private class Lead
        {
            public Lead(Label unknown, Edge remove, Edge place)
            {
                Unknown = unknown;
                Remove = remove;
                Place = place;
            }

            public Label Unknown { get; }
            public Edge Remove { get; }
            public Edge Place { get; }
        }

        /// <summary><c>r-&gt;R = u-&gt;L</c> instead of <c>u-&gt;L = r-&gt;R</c>.</summary>
        private static Edge Reverse(Edge edge)
        {
            return new Edge(
                edge.Condition.LabelRight,
                new PathCondition(edge.Condition.RolesRight, edge.Home, edge.Condition.RolesLeft));
        }

        /// <summary>
        /// Labels the matches name but do not bind; <paramref name="inner"/> holds the
        /// labels bound around them.
        /// </summary>
        private static IEnumerable<string> FreeInMatches(ImmutableList<string> inner, ImmutableList<Match> matches)
        {
            return matches.SelectMany(match => FreeInConditions(inner, match));
        }

        private static IEnumerable<string> FreeInConditions(ImmutableList<string> inner, Match match)
        {
            return match.PathConditions
                .Where(condition => !inner.Contains(condition.LabelRight))
                .Select(condition => condition.LabelRight)
                .Concat(match.ExistentialConditions.SelectMany(condition =>
                    FreeInMatches(
                        inner.AddRange(condition.Matches.Select(inner2 => inner2.Unknown.Name)),
                        condition.Matches)));
        }

        private static IEnumerable<string> FreeLabels(ExistentialCondition condition)
        {
            return FreeInMatches(
                condition.Matches.Select(match => match.Unknown.Name).ToImmutableList(),
                condition.Matches);
        }

        /// <summary>
        /// First what the new fact reaches, then what the givens reach; <c>null</c> if
        /// anything is left.
        /// </summary>
        private static ImmutableList<Match>? OrderFromPin(ImmutableList<string> givens, ImmutableList<Match> matches)
        {
            var labels = matches.Select(match => match.Unknown).ToImmutableList();
            var edges = matches
                .SelectMany(match => match.PathConditions
                    .Select(condition => new Edge(match.Unknown.Name, condition)))
                .ToImmutableList();
            var filters = matches
                .SelectMany(match => match.ExistentialConditions)
                .ToImmutableList();

            Lead? Reaching(ImmutableList<string> reach, Label unknown, Edge edge)
            {
                if (edge.Home == unknown.Name && reach.Contains(edge.Condition.LabelRight))
                {
                    return new Lead(unknown, edge, edge);
                }
                if (edge.Condition.LabelRight == unknown.Name && reach.Contains(edge.Home))
                {
                    return new Lead(unknown, edge, Reverse(edge));
                }
                return null;
            }

            (ImmutableList<string> bound, ImmutableList<Match> placed) Grow(ImmutableList<string> reach, ImmutableList<string> bound)
            {
                var placed = ImmutableList<Match>.Empty;
                for (int remaining = matches.Count; remaining > 0; remaining--)
                {
                    Lead? lead = null;
                    foreach (var unknown in labels)
                    {
                        foreach (var edge in edges)
                        {
                            lead = Reaching(reach, unknown, edge);
                            if (lead != null)
                            {
                                break;
                            }
                        }
                        if (lead != null)
                        {
                            break;
                        }
                    }
                    if (lead == null)
                    {
                        break;
                    }

                    var found = lead.Unknown;
                    bound = bound.Insert(0, found.Name);
                    reach = reach.Insert(0, found.Name);
                    var boundHere = bound;
                    bool Ready(Edge edge) =>
                        boundHere.Contains(edge.Home) && boundHere.Contains(edge.Condition.LabelRight);
                    bool IsReady(ExistentialCondition condition) =>
                        FreeLabels(condition).All(label => boundHere.Contains(label));

                    edges = RemoveFirst(edges, lead.Remove, SameEdge);
                    var attached = edges.Where(Ready).ToImmutableList();
                    var attachedFilters = filters.Where(IsReady).ToImmutableList();
                    edges = edges.Where(edge => !Ready(edge)).ToImmutableList();
                    filters = filters.Where(condition => !IsReady(condition)).ToImmutableList();
                    labels = labels.Remove(found);

                    var home = found.Name;
                    placed = placed.Add(new Match(
                        found,
                        ImmutableList.Create(lead.Place.Condition).AddRange(attached
                            .Select(edge => (edge.Home == home ? edge : Reverse(edge)).Condition)),
                        attachedFilters));
                }
                return (bound, placed);
            }

            var fromPin = Grow(ImmutableList.Create(PinLabel), givens.Insert(0, PinLabel));
            var fromGivens = Grow(fromPin.bound, fromPin.bound);
            return labels.IsEmpty && edges.IsEmpty && filters.IsEmpty
                ? fromPin.placed.AddRange(fromGivens.placed)
                : null;
        }

        /// <summary>
        /// Every path condition names a label in scope; a match's unknown is in scope for
        /// its existential conditions.
        /// </summary>
        private static bool IsScoped(ImmutableList<string> scope, ImmutableList<Match> matches)
        {
            foreach (var match in matches)
            {
                var inner = scope.Insert(0, match.Unknown.Name);
                if (match.PathConditions.Any(condition => !scope.Contains(condition.LabelRight)))
                {
                    return false;
                }
                if (match.ExistentialConditions.Any(condition => !IsScoped(inner, condition.Matches)))
                {
                    return false;
                }
                scope = inner;
            }
            return true;
        }

        private static bool BindsOnce(ImmutableList<string> scope, ImmutableList<Match> matches)
        {
            var names = matches.Select(match => match.Unknown.Name).ToImmutableList();
            return names.Distinct().Count() == names.Count &&
                names.All(name => !scope.Contains(name)) &&
                IsScoped(scope, matches);
        }

        /// <summary>
        /// A condition and the match it is written on. Exactly one of
        /// <see cref="Path"/> and <see cref="Existential"/> is set.
        /// </summary>
        private class Constraint
        {
            public Constraint(string home, PathCondition? path, ExistentialCondition? existential)
            {
                Home = home;
                Path = path;
                Existential = existential;
            }

            public string Home { get; }
            public PathCondition? Path { get; }
            public ExistentialCondition? Existential { get; }
        }

        /// <summary>
        /// As written; a path condition reversed onto its other end; an existential
        /// condition on any match.
        /// </summary>
        private static bool SameConstraint(Constraint left, Constraint right)
        {
            if (left.Path != null && right.Path != null)
            {
                return (left.Home == right.Home && SamePath(left.Path, right.Path)) ||
                    (right.Home == left.Path.LabelRight &&
                        SameEdge(
                            Reverse(new Edge(left.Home, left.Path)),
                            new Edge(right.Home, right.Path)));
            }
            if (left.Existential != null && right.Existential != null)
            {
                return SameExistential(left.Existential, right.Existential);
            }
            return false;
        }

        private static IEnumerable<Constraint> Constraints(ImmutableList<Match> matches)
        {
            return matches.SelectMany(match => match.PathConditions
                .Select(condition => new Constraint(match.Unknown.Name, condition, null))
                .Concat(match.ExistentialConditions
                    .Select(condition => new Constraint(match.Unknown.Name, null, condition))));
        }

        private static bool Covers(ImmutableList<Match> left, ImmutableList<Match> right)
        {
            var rightConstraints = Constraints(right).ToImmutableList();
            return Constraints(left).All(constraint =>
                rightConstraints.Any(other => SameConstraint(constraint, other)));
        }

        /// <summary>
        /// <paramref name="right"/> is an arrangement of <paramref name="left"/>: the same
        /// unknowns, each bound once, and the same conditions.
        /// </summary>
        private static bool Arranges(ImmutableList<string> scope, ImmutableList<Match> left, ImmutableList<Match> right)
        {
            return BindsOnce(scope, left) && BindsOnce(scope, right) &&
                left.All(match => right.Any(other => match.Unknown == other.Unknown)) &&
                right.All(match => left.Any(other => match.Unknown == other.Unknown)) &&
                Covers(left, right) && Covers(right, left);
        }

        /// <summary>Every label the projection reads from this level.</summary>
        private static IEnumerable<string> ProjectionLabels(Projection projection)
        {
            switch (projection)
            {
                case SimpleProjection simple:
                    return ImmutableList.Create(simple.Tag);
                case FieldProjection field:
                    return ImmutableList.Create(field.Tag);
                case HashProjection hash:
                    return ImmutableList.Create(hash.Tag);
                case SignerProjection signer:
                    return ImmutableList.Create(signer.Tag);
                case DateLearnedProjection dateLearned:
                    return ImmutableList.Create(dateLearned.Tag);
                case CompoundProjection compound:
                    // A collection reads the labels its own matches leave free; any other
                    // component reads whatever labels it names, which for a nested
                    // compound are its own components'.
                    return compound.Names.SelectMany(name =>
                        compound.GetProjection(name) is CollectionProjection collection
                            ? FreeInMatches(
                                collection.Matches.Select(match => match.Unknown.Name).ToImmutableList(),
                                collection.Matches)
                            : ProjectionLabels(compound.GetProjection(name)));
                default:
                    return ImmutableList<string>.Empty;
            }
        }

        /* ------------- Structural equality: never reference equality ------------- */

        private static ImmutableList<T> RemoveFirst<T>(ImmutableList<T> list, T item, Func<T, T, bool> same)
            where T : class
        {
            int index = list.FindIndex(other => same(item, other));
            return index < 0 ? list : list.RemoveAt(index);
        }

        private static bool SameRoles(ImmutableList<Role> left, ImmutableList<Role> right)
        {
            return left.Count == right.Count &&
                left.Zip(right, (a, b) => a.Name == b.Name && a.TargetType == b.TargetType).All(same => same);
        }

        private static bool SamePath(PathCondition left, PathCondition right)
        {
            return left.LabelRight == right.LabelRight &&
                SameRoles(left.RolesLeft, right.RolesLeft) &&
                SameRoles(left.RolesRight, right.RolesRight);
        }

        private static bool SameEdge(Edge left, Edge right)
        {
            return left.Home == right.Home && SamePath(left.Condition, right.Condition);
        }

        private static bool SameExistential(ExistentialCondition left, ExistentialCondition right)
        {
            return left.Exists == right.Exists && SameMatches(left.Matches, right.Matches);
        }

        private static bool SameMatches(ImmutableList<Match> left, ImmutableList<Match> right)
        {
            return left.Count == right.Count && left.Zip(right, SameMatch).All(same => same);
        }

        private static bool SameMatch(Match left, Match right)
        {
            return left.Unknown == right.Unknown &&
                left.PathConditions.Count == right.PathConditions.Count &&
                left.PathConditions.Zip(right.PathConditions, SamePath).All(same => same) &&
                left.ExistentialConditions.Count == right.ExistentialConditions.Count &&
                left.ExistentialConditions.Zip(right.ExistentialConditions, SameExistential).All(same => same);
        }
    }
}
