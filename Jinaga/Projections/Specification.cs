using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Products;
using System;
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
            var projection = this.Projection == null ? "" : " => " + this.Projection.ToDescriptiveString(depth);
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
            var pathCondition = match.PathConditions.Single();
            var result = ExecutePathCondition(references, match.Unknown, pathCondition, graph);
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

        internal Specification Reduce()
        {
            // TODO: Remove all projections except for specification projections.
            return this;
        }
    }
}
