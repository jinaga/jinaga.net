using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    /// <summary>
    /// The skeleton of a feed: the shape a replicator compares a requested feed against the
    /// feeds of its distribution rules by.
    ///
    /// Ported from <c>skeletonOfSpecification</c> in jinaga.js
    /// (<c>src/specification/skeleton.ts</c>), at jinaga.js
    /// <c>a8ad06e15227838c2c0d4a4d462a3b7dd39885e3</c>, which is the commit
    /// <c>Jinaga.Test/Conformance/Reference</c> is pinned at. jinaga.js is the reference here:
    /// jinaga-spec does not define skeletons, so there is no Lean definition to follow and no
    /// theorem to lean on. The invariants jinaga.js states for its own skeletons are in its
    /// <c>docs/analysis/specification-invariants.md</c>.
    ///
    /// <para>
    /// <b>This is deliberately not canonical.</b> <c>factIndex</c> and <c>edgeIndex</c> are
    /// assigned by position in the walk, so two specifications that mean the same thing but
    /// are written in a different order get different skeletons (L7a and O7 in jinaga.js's
    /// <c>specification-invariants.md</c>, both still open there). Making the port canonical
    /// would make it equate feeds the replicator does not, and so grant distribution the
    /// replicator refuses. The walk below therefore reproduces the reference's order and its
    /// positional indices exactly, defects included.
    /// </para>
    /// </summary>
    internal class Skeleton
    {
        public Skeleton(
            ImmutableList<FactDescription> facts,
            ImmutableList<InputDescription> inputs,
            ImmutableList<EdgeDescription> edges,
            ImmutableList<NotExistsConditionDescription> notExistsConditions,
            ImmutableList<OutputDescription> outputs)
        {
            Facts = facts;
            Inputs = inputs;
            Edges = edges;
            NotExistsConditions = notExistsConditions;
            Outputs = outputs;
        }

        public ImmutableList<FactDescription> Facts { get; }
        public ImmutableList<InputDescription> Inputs { get; }
        public ImmutableList<EdgeDescription> Edges { get; }
        public ImmutableList<NotExistsConditionDescription> NotExistsConditions { get; }
        public ImmutableList<OutputDescription> Outputs { get; }

        public static readonly Skeleton Empty = new Skeleton(
            ImmutableList<FactDescription>.Empty,
            ImmutableList<InputDescription>.Empty,
            ImmutableList<EdgeDescription>.Empty,
            ImmutableList<NotExistsConditionDescription>.Empty,
            ImmutableList<OutputDescription>.Empty);

        /// <summary>
        /// The skeleton of a specification (<c>skeletonOfSpecification</c>).
        /// </summary>
        /// <remarks>
        /// jinaga.js walks the conditions of a match in the one order they are written in,
        /// path and existential conditions interleaved. <see cref="Match"/> holds the two
        /// kinds apart, so this walks every path condition of a match and then every
        /// existential condition of it. No specification this library can build distinguishes
        /// the two orders, because <see cref="Match"/> cannot hold an interleaving; a
        /// jinaga.js specification that writes an existential condition before a path
        /// condition of the same match is the one shape whose skeleton this would index
        /// differently, and <c>Conformance/README.md</c> records that none of the pinned
        /// vectors has it.
        /// </remarks>
        public static Skeleton Of(Specification specification)
        {
            var givenFacts = specification.Givens
                .Select((given, index) => (given.Label.Name, Type: given.Label.Type, InputIndex: index))
                .ToImmutableDictionary(given => given.Name, given => (given.Type, given.InputIndex));
            var (skeleton, _) = AddEdges(
                Empty,
                givenFacts,
                ImmutableDictionary<string, FactDescription>.Empty,
                ImmutableList<int>.Empty,
                specification.Matches);
            return skeleton;
        }

        /* ----------------------------------- The walk ----------------------------------- */

        // addEdges. A positive condition's edges join the skeleton at the current path; a
        // negative condition opens a new not-exists condition and walks its matches beneath
        // it. Either way the labels the condition bound go out of scope with it, so only the
        // skeleton is carried out, exactly as the reference discards the inner knownFacts.
        private static (Skeleton Skeleton, ImmutableDictionary<string, FactDescription> KnownFacts) AddEdges(
            Skeleton skeleton,
            ImmutableDictionary<string, (string Type, int InputIndex)> givenFacts,
            ImmutableDictionary<string, FactDescription> knownFacts,
            ImmutableList<int> path,
            ImmutableList<Match> matches)
        {
            foreach (var match in matches)
            {
                foreach (var condition in match.PathConditions)
                {
                    (skeleton, knownFacts) = AddPathCondition(
                        skeleton, givenFacts, knownFacts, path, match.Unknown, condition);
                }
                foreach (var condition in match.ExistentialConditions)
                {
                    if (condition.Exists)
                    {
                        (skeleton, _) = AddEdges(skeleton, givenFacts, knownFacts, path, condition.Matches);
                    }
                    else
                    {
                        var (opened, conditionalPath) = WithNotExistsCondition(skeleton, path);
                        (skeleton, _) = AddEdges(opened, givenFacts, knownFacts, conditionalPath, condition.Matches);
                    }
                }
            }
            return (skeleton, knownFacts);
        }

        // addPathCondition.
        private static (Skeleton Skeleton, ImmutableDictionary<string, FactDescription> KnownFacts) AddPathCondition(
            Skeleton skeleton,
            ImmutableDictionary<string, (string Type, int InputIndex)> givenFacts,
            ImmutableDictionary<string, FactDescription> knownFacts,
            ImmutableList<int> path,
            Pipelines.Label unknown,
            PathCondition condition)
        {
            // A given enters the skeleton as an input the first time a condition names it.
            if (givenFacts.TryGetValue(condition.LabelRight, out var given) &&
                !knownFacts.ContainsKey(condition.LabelRight))
            {
                skeleton = WithInput(skeleton, given.Type, given.InputIndex);
                knownFacts = knownFacts.Add(
                    condition.LabelRight, new FactDescription(given.Type, skeleton.Facts.Count));
            }

            // Whether the unknown has already been written, which decides whether the last
            // role of the condition joins to it or writes a new fact.
            var knownFact = knownFacts.TryGetValue(unknown.Name, out var written) ? written : null;
            var roleCount = condition.RolesLeft.Count + condition.RolesRight.Count;

            if (!knownFacts.TryGetValue(condition.LabelRight, out var fact))
            {
                throw new SpecificationException(
                    $"Label {condition.LabelRight} not found. " +
                    $"Known labels: {string.Join(", ", knownFacts.Keys)}");
            }
            var factType = fact.FactType;
            var factIndex = fact.FactIndex;

            // Walk up the right-hand side, generating predecessor joins.
            for (var i = 0; i < condition.RolesRight.Count; i++)
            {
                var role = condition.RolesRight[i];
                if (i == roleCount - 1 && knownFact != null)
                {
                    skeleton = WithEdge(skeleton, knownFact.FactIndex, factIndex, role.Name, path);
                    factIndex = knownFact.FactIndex;
                }
                else
                {
                    var (withFact, predecessorFactIndex) = WithFact(skeleton, role.TargetType);
                    skeleton = WithEdge(withFact, predecessorFactIndex, factIndex, role.Name, path);
                    factIndex = predecessorFactIndex;
                }
                factType = role.TargetType;
            }

            var rightType = factType;

            // Walk up the left-hand side, collecting the successor joins to be made in
            // reverse.
            factType = unknown.Type;
            var newEdges = new List<(string RoleName, string SuccessorType)>();
            foreach (var role in condition.RolesLeft)
            {
                newEdges.Add((role.Name, factType));
                factType = role.TargetType;
            }

            if (factType != rightType)
            {
                throw new SpecificationException($"Type mismatch: {factType} is compared to {rightType}");
            }

            newEdges.Reverse();
            for (var i = 0; i < newEdges.Count; i++)
            {
                var (roleName, successorType) = newEdges[i];
                if (condition.RolesRight.Count + i == roleCount - 1 && knownFact != null)
                {
                    skeleton = WithEdge(skeleton, factIndex, knownFact.FactIndex, roleName, path);
                    factIndex = knownFact.FactIndex;
                }
                else
                {
                    var (withFact, successorFactIndex) = WithFact(skeleton, successorType);
                    skeleton = WithEdge(withFact, factIndex, successorFactIndex, roleName, path);
                    factIndex = successorFactIndex;
                }
            }

            if (knownFact == null)
            {
                knownFacts = knownFacts.Add(unknown.Name, new FactDescription(unknown.Type, factIndex));
                // Only an unknown matched outside every existential condition is an output.
                if (path.IsEmpty)
                {
                    skeleton = WithOutput(skeleton, factIndex);
                }
            }

            return (skeleton, knownFacts);
        }

        /* -------------------------------- Building it up -------------------------------- */

        // withFact. The index is the fact's position, counting from one.
        private static (Skeleton Skeleton, int FactIndex) WithFact(Skeleton skeleton, string factType)
        {
            var factIndex = skeleton.Facts.Count + 1;
            return (
                new Skeleton(
                    skeleton.Facts.Add(new FactDescription(factType, factIndex)),
                    skeleton.Inputs,
                    skeleton.Edges,
                    skeleton.NotExistsConditions,
                    skeleton.Outputs),
                factIndex);
        }

        // withInput.
        private static Skeleton WithInput(Skeleton skeleton, string factType, int inputIndex)
        {
            var (withFact, factIndex) = WithFact(skeleton, factType);
            return new Skeleton(
                withFact.Facts,
                withFact.Inputs.Add(new InputDescription(factIndex, inputIndex)),
                withFact.Edges,
                withFact.NotExistsConditions,
                withFact.Outputs);
        }

        // withEdge. The edge index counts every edge written so far, the top-level ones and
        // those inside every not-exists condition alike, so it is unique across the skeleton.
        private static Skeleton WithEdge(
            Skeleton skeleton, int predecessorFactIndex, int successorFactIndex, string roleName,
            ImmutableList<int> path)
        {
            var edgeIndex = skeleton.Edges.Count + CountEdges(skeleton.NotExistsConditions) + 1;
            var edge = new EdgeDescription(edgeIndex, predecessorFactIndex, successorFactIndex, roleName);
            if (path.IsEmpty)
            {
                return new Skeleton(
                    skeleton.Facts,
                    skeleton.Inputs,
                    skeleton.Edges.Add(edge),
                    skeleton.NotExistsConditions,
                    skeleton.Outputs);
            }
            return new Skeleton(
                skeleton.Facts,
                skeleton.Inputs,
                skeleton.Edges,
                NotExistsWithEdge(skeleton.NotExistsConditions, edge, path),
                skeleton.Outputs);
        }

        // withOutput.
        private static Skeleton WithOutput(Skeleton skeleton, int factIndex) =>
            new Skeleton(
                skeleton.Facts,
                skeleton.Inputs,
                skeleton.Edges,
                skeleton.NotExistsConditions,
                skeleton.Outputs.Add(new OutputDescription(factIndex)));

        // withNotExistsCondition. Appends an empty condition at the end of the list the path
        // names, and returns the path that reaches it.
        private static (Skeleton Skeleton, ImmutableList<int> Path) WithNotExistsCondition(
            Skeleton skeleton, ImmutableList<int> path)
        {
            var (conditions, newPath) = NotExistsWithCondition(skeleton.NotExistsConditions, path);
            return (
                new Skeleton(
                    skeleton.Facts,
                    skeleton.Inputs,
                    skeleton.Edges,
                    conditions,
                    skeleton.Outputs),
                newPath);
        }

        // notExistsWithEdge.
        private static ImmutableList<NotExistsConditionDescription> NotExistsWithEdge(
            ImmutableList<NotExistsConditionDescription> conditions,
            EdgeDescription edge,
            ImmutableList<int> path)
        {
            var index = path[0];
            var condition = conditions[index];
            var replacement = path.Count == 1
                ? new NotExistsConditionDescription(condition.Edges.Add(edge), condition.NotExistsConditions)
                : new NotExistsConditionDescription(
                    condition.Edges,
                    NotExistsWithEdge(condition.NotExistsConditions, edge, path.RemoveAt(0)));
            return conditions.SetItem(index, replacement);
        }

        // notExistsWithCondition.
        private static (ImmutableList<NotExistsConditionDescription> Conditions, ImmutableList<int> Path)
            NotExistsWithCondition(
                ImmutableList<NotExistsConditionDescription> conditions, ImmutableList<int> path)
        {
            if (path.IsEmpty)
            {
                return (
                    conditions.Add(new NotExistsConditionDescription(
                        ImmutableList<EdgeDescription>.Empty,
                        ImmutableList<NotExistsConditionDescription>.Empty)),
                    ImmutableList.Create(conditions.Count));
            }
            var index = path[0];
            var condition = conditions[index];
            var (nested, nestedPath) = NotExistsWithCondition(
                condition.NotExistsConditions, path.RemoveAt(0));
            return (
                conditions.SetItem(index, new NotExistsConditionDescription(condition.Edges, nested)),
                nestedPath.Insert(0, index));
        }

        // countEdges.
        private static int CountEdges(ImmutableList<NotExistsConditionDescription> conditions) =>
            conditions.Sum(condition => condition.Edges.Count + CountEdges(condition.NotExistsConditions));
    }

    internal class FactDescription
    {
        public FactDescription(string factType, int factIndex)
        {
            FactType = factType;
            FactIndex = factIndex;
        }

        public string FactType { get; }
        public int FactIndex { get; }

        public override string ToString() => $"{FactIndex}: {FactType}";
    }

    internal class InputDescription
    {
        public InputDescription(int factIndex, int inputIndex)
        {
            FactIndex = factIndex;
            InputIndex = inputIndex;
        }

        public int FactIndex { get; }
        public int InputIndex { get; }

        public override string ToString() => $"input {InputIndex} is fact {FactIndex}";
    }

    internal class EdgeDescription
    {
        public EdgeDescription(int edgeIndex, int predecessorFactIndex, int successorFactIndex, string roleName)
        {
            EdgeIndex = edgeIndex;
            PredecessorFactIndex = predecessorFactIndex;
            SuccessorFactIndex = successorFactIndex;
            RoleName = roleName;
        }

        public int EdgeIndex { get; }
        public int PredecessorFactIndex { get; }
        public int SuccessorFactIndex { get; }
        public string RoleName { get; }

        public override string ToString() =>
            $"{EdgeIndex}: {PredecessorFactIndex}->{RoleName}->{SuccessorFactIndex}";
    }

    internal class NotExistsConditionDescription
    {
        public NotExistsConditionDescription(
            ImmutableList<EdgeDescription> edges,
            ImmutableList<NotExistsConditionDescription> notExistsConditions)
        {
            Edges = edges;
            NotExistsConditions = notExistsConditions;
        }

        public ImmutableList<EdgeDescription> Edges { get; }
        public ImmutableList<NotExistsConditionDescription> NotExistsConditions { get; }

        public override string ToString() =>
            $"!E {{ {string.Join(", ", Edges)}{(NotExistsConditions.IsEmpty ? "" : " ...")} }}";
    }

    internal class OutputDescription
    {
        public OutputDescription(int factIndex)
        {
            FactIndex = factIndex;
        }

        public int FactIndex { get; }

        public override string ToString() => $"output {FactIndex}";
    }
}
