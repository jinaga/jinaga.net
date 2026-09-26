using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Products;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    public class Specification
    {
        public Specification(
            ImmutableList<SpecificationGiven> givens,
            ImmutableList<Match> matches,
            Projection projection)
        {
            Givens = givens;
            Matches = matches;
            Projection = projection;
        }

        public ImmutableList<SpecificationGiven> Givens { get; }
        public ImmutableList<Match> Matches { get; }
        public Projection Projection { get; }

        public bool CanRunOnGraph =>
            Givens.All(g => g.CanRunOnGraph) &&
            Matches.All(m => m.CanRunOnGraph) &&
            Projection.CanRunOnGraph;

        public Specification Apply(ImmutableList<string> arguments)
        {
            var replacements = Givens.Zip(arguments, (parameter, argument) => (parameter.Label, argument))
                .ToImmutableDictionary(pair => pair.Label.Name, pair => pair.argument);
            var newMatches = Matches
                .Select(match =>
                    match.Apply(replacements)
                )
                .ToImmutableList();
            var newProjection = Projection.Apply(replacements);
            return new Specification(ImmutableList<SpecificationGiven>.Empty, newMatches, newProjection);
        }

        public Specification WithProjection(Projection projection)
        {
            return new Specification(Givens, Matches, projection);
        }

        public ImmutableList<Product> Execute(FactReferenceTuple givenTuple, FactGraph graph)
        {
            var tuples = ExecuteMatches(givenTuple, Matches, graph);
            var products = tuples.Select(tuple => CreateProduct(tuple, Projection, graph)).ToImmutableList();
            return products;
        }

        public ImmutableList<Inverse> ComputeInverses()
        {
            return Inverter.InvertSpecification(this);
        }

        public string ToDescriptiveString(int depth = 0)
        {
            var indent = new string(' ', depth * 4);
            var given = string.Join(", ", this.Givens.Select(g => $"{g.Label.Name}: {g.Label.Type}{Conditions(g.ExistentialConditions, depth)}"));
            var matches = string.Join("", this.Matches.Select(m => m.ToDescriptiveString(depth + 1)));
            var projection = this.Projection is CompoundProjection cp && !cp.Names.Any() ? "" : " => " + this.Projection.ToDescriptiveString(depth);
            return $"{indent}({given}) {{\n{matches}{indent}}}{projection}\n";
        }

        private string Conditions(ImmutableList<ExistentialCondition> existentialConditions, int depth)
        {
            if (existentialConditions.Count == 0)
            {
                return string.Empty;
            }
            var indent = new string(' ', depth * 4);
            var conditions = string.Join("", existentialConditions.Select(c => c.ToDescriptiveString(this.Givens.First().Label.Name, depth + 1)));
            return $" [\n{conditions}{indent}]";
        }

        internal string GenerateDeclarationString(FactReferenceTuple given)
        {
            var startStrings = Givens.Select(g =>
            {
                var reference = given.Get(g.Label.Name);
                return $"let {g.Label.Name}: {g.Label.Type} = #{reference.Hash}\n";
            });
            return string.Join("", startStrings);
        }

        public override string ToString()
        {
            return ToDescriptiveString();
        }

        private static ImmutableList<FactReferenceTuple> ExecuteMatches(FactReferenceTuple start, ImmutableList<Match> matches, FactGraph graph)
        {
            return matches.Aggregate(
                ImmutableList.Create(start),
                (set, match) => set
                    .SelectMany(references => ExecuteMatch(references, match, graph))
                    .ToImmutableList());
        }

        private static Product CreateProduct(FactReferenceTuple tuple, Projection projection, FactGraph graph)
        {
            var product = tuple.Names.Aggregate(
                Product.Empty,
                (product, name) => product.With(name, new SimpleElement(tuple.Get(name)))
            );
            product = ExecuteProjection(tuple, product, projection, graph);
            return product;
        }

        private static Product ExecuteProjection(FactReferenceTuple tuple, Product product, Projection projection, FactGraph graph)
        {
            if (projection is CompoundProjection compoundProjection)
            {
                foreach (var name in compoundProjection.Names)
                {
                    var childProjection = compoundProjection.GetProjection(name);
                    if (childProjection is SimpleProjection simpleProjection)
                    {
                        var element = new SimpleElement(tuple.Get(simpleProjection.Tag));
                        product = product.With(name, element);
                    }
                    else if (childProjection is CollectionProjection collectionProjection)
                    {
                        var tuples = ExecuteMatches(tuple, collectionProjection.Matches, graph);
                        var products = tuples.Select(tuple => CreateProduct(tuple, collectionProjection.Projection, graph)).ToImmutableList();
                        var element = new CollectionElement(products);
                        product = product.With(name, element);
                    }
                    else if (childProjection is FieldProjection fieldProjection)
                    {
                        var element = new SimpleElement(tuple.Get(fieldProjection.Tag));
                        product = product.With(name, element);
                    }
                    else
                    {
                        throw new Exception($"Unsupported projection type {childProjection.GetType().Name}.");
                    }
                }
            }
            return product;
        }

        private static ImmutableList<FactReferenceTuple> ExecuteMatch(FactReferenceTuple references, Match match, FactGraph graph)
        {
            // Every path condition must hold, so the unknown is the intersection of their results.
            var result = match.PathConditions
                .Skip(1)
                .Aggregate(
                    ExecutePathCondition(references, match.Unknown, match.PathConditions[0], graph),
                    (set, pathCondition) => set
                        .Intersect(ExecutePathCondition(references, match.Unknown, pathCondition, graph))
                        .ToImmutableList());
            var resultReferences = result.Select(reference =>
                references.Add(match.Unknown.Name, reference)).ToImmutableList();
            return resultReferences;
        }

        private static ImmutableList<FactReference> ExecutePathCondition(FactReferenceTuple start, Label unknown, PathCondition pathCondition, FactGraph graph)
        {
            var startingFactReference = start.Get(pathCondition.LabelRight);
            var set = ImmutableList.Create(startingFactReference);
            foreach (var role in pathCondition.RolesRight)
            {
                set = ExecutePredecessorStep(set, role.Name, role.TargetType, graph);
            }
            return set;
        }

        private static ImmutableList<FactReference> ExecutePredecessorStep(ImmutableList<FactReference> set, string role, string targetType, FactGraph graph)
        {
            return set.SelectMany(reference => graph.Predecessors(reference, role, targetType))
                .ToImmutableList();
        }

        /// <summary>
        /// Splits the specification before the first match that seeks successors.
        ///
        /// The head contains only predecessor joins, so it can run on a fact graph that has not
        /// been saved. The tail runs on the store, given the labels of the head that it needs.
        /// Either may be null: no head means the specification starts with a successor join, and
        /// no tail means the whole specification is deterministic.
        /// </summary>
        internal (Specification? head, Specification? tail) SplitBeforeFirstSuccessor()
        {
            // A match is deterministic when every one of its path conditions walks only
            // predecessors. Several such conditions intersect, which the graph can still run.
            var pivotIndex = Matches.FindIndex(match =>
                match.PathConditions.Count == 0 ||
                match.ExistentialConditions.Count != 0 ||
                match.PathConditions.Any(condition => condition.RolesLeft.Count != 0));

            if (pivotIndex == -1)
            {
                // No match seeks successors, so the whole specification is deterministic.
                return (this, null);
            }

            var pivot = Matches[pivotIndex];
            if (pivot.PathConditions.Count != 1)
            {
                return (null, this);
            }

            var condition = pivot.PathConditions[0];
            var unknownsAsGivens = Matches
                .Select(match => new SpecificationGiven(match.Unknown, ImmutableList<ExistentialCondition>.Empty));

            if (condition.RolesRight.Count == 0)
            {
                // The path contains only successor joins. Put the entire match in the tail.
                if (pivotIndex == 0)
                {
                    return (null, this);
                }

                var headMatches = Matches.GetRange(0, pivotIndex);
                var tailMatches = Matches.GetRange(pivotIndex, Matches.Count - pivotIndex);
                var head = new Specification(
                    ReferencedLabels(headMatches, CompoundProjection.Empty, Givens),
                    headMatches,
                    CompoundProjection.Empty);
                var tail = new Specification(
                    ReferencedLabels(tailMatches, Projection, Givens.AddRange(unknownsAsGivens)),
                    tailMatches,
                    Projection);
                return (head, tail);
            }
            else
            {
                // The path contains both predecessor and successor joins. Split it at a new label.
                var usedNames = Givens.Select(g => g.Label.Name)
                    .Concat(Matches.Select(m => m.Unknown.Name))
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

                var headMatches = Matches.GetRange(0, pivotIndex).Add(headMatch);
                var tailMatches = Matches.GetRange(pivotIndex + 1, Matches.Count - pivotIndex - 1)
                    .Insert(0, tailMatch);
                var allLabels = Givens
                    .AddRange(unknownsAsGivens)
                    .Add(new SpecificationGiven(splitLabel, ImmutableList<ExistentialCondition>.Empty));
                var head = new Specification(
                    ReferencedLabels(headMatches, CompoundProjection.Empty, Givens),
                    headMatches,
                    CompoundProjection.Empty);
                var tail = new Specification(
                    ReferencedLabels(tailMatches, Projection, allLabels),
                    tailMatches,
                    Projection);
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

        internal Specification Reduce()
        {
            // TODO: Remove all projections except for specification projections.
            return this;
        }
    }
}
