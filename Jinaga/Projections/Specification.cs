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
            ImmutableList<Label> given,
            ImmutableList<Match> matches,
            Projection projection)
        {
            Given = given;
            Matches = matches;
            Projection = projection;
        }

        public ImmutableList<Label> Given { get; }
        public ImmutableList<Match> Matches { get; }
        public Projection Projection { get; }

        public bool CanRunOnGraph => Matches.All(m => m.CanRunOnGraph) && Projection.CanRunOnGraph;

        public Specification Apply(ImmutableList<string> arguments)
        {
            var replacements = Given.Zip(arguments, (parameter, argument) => (parameter, argument))
                .ToImmutableDictionary(pair => pair.parameter.Name, pair => pair.argument);
            var newMatches = Matches.Select(match => match.Apply(replacements)).ToImmutableList();
            var newProjection = Projection.Apply(replacements);
            return new Specification(ImmutableList<Label>.Empty, newMatches, newProjection);
        }

        public ImmutableList<Product> Execute(ImmutableList<FactReference> givenReferences, FactGraph graph)
        {
            var start = Given.Zip(givenReferences, (given, reference) =>
                (name: given.Name, reference)
            ).ToImmutableDictionary(pair => pair.name, pair => pair.reference);
            var tuples = ExecuteMatches(start, Matches, graph);
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
            var given = string.Join(", ", this.Given.Select(g => $"{g.Name}: {g.Type}"));
            var matches = string.Join("", this.Matches.Select(m => m.ToDescriptiveString(depth + 1)));
            var projection = this.Projection == null ? "" : " => " + this.Projection.ToDescriptiveString(depth);
            return $"{indent}({given}) {{\n{matches}{indent}}}{projection}\n";
        }

        public override string ToString()
        {
            return ToDescriptiveString();
        }

        private static ImmutableList<ImmutableDictionary<string, FactReference>> ExecuteMatches(ImmutableDictionary<string, FactReference> start, ImmutableList<Match> matches, FactGraph graph)
        {
            return matches.Aggregate(
                ImmutableList.Create(start),
                (set, match) => set
                    .SelectMany(references => ExecuteMatch(references, match, graph))
                    .ToImmutableList());
        }

        private static Product CreateProduct(ImmutableDictionary<string, FactReference> tuple, Projection projection, FactGraph graph)
        {
            var product = tuple.Keys.Aggregate(
                Product.Empty,
                (product, name) => product.With(name, new SimpleElement(tuple[name]))
            );
            product = ExecuteProjection(tuple, product, projection, graph);
            return product;
        }

        private static Product ExecuteProjection(ImmutableDictionary<string, FactReference> tuple, Product product, Projection projection, FactGraph graph)
        {
            if (projection is CompoundProjection compoundProjection)
            {
                foreach (var name in compoundProjection.Names)
                {
                    var childProjection = compoundProjection.GetProjection(name);
                    if (childProjection is SimpleProjection simpleProjection)
                    {
                        var element = new SimpleElement(tuple[simpleProjection.Tag]);
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
                        var element = new SimpleElement(tuple[fieldProjection.Tag]);
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

        private static ImmutableList<ImmutableDictionary<string, FactReference>> ExecuteMatch(ImmutableDictionary<string, FactReference> references, Match match, FactGraph graph)
        {
            var condition = match.Conditions.Single();
            if (condition is PathCondition pathCondition)
            {
                var result = ExecutePathCondition(references, match.Unknown, pathCondition, graph);
                var resultReferences = result.Select(reference =>
                    references.Add(match.Unknown.Name, reference)).ToImmutableList();
                return resultReferences;
            }
            else
            {
                throw new ArgumentException("The first condition must be a path condition.");
            }
        }

        private static ImmutableList<FactReference> ExecutePathCondition(ImmutableDictionary<string, FactReference> start, Label unknown, PathCondition pathCondition, FactGraph graph)
        {
            if (!start.ContainsKey(pathCondition.LabelRight))
            {
                var keys = string.Join(", ", start.Keys);
                throw new ArgumentException($"The label {pathCondition.LabelRight} is not in the start set: {keys}.");
            }
            var startingFactReference = start[pathCondition.LabelRight];
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
    }
}
