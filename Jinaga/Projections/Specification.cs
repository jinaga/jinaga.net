using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Products;

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

        public Specification Apply(ImmutableList<Label> arguments)
        {
            var replacements = Given.Zip(arguments, (parameter, argument) => (parameter, argument))
                .ToImmutableDictionary(pair => pair.parameter.Name, pair => pair.argument.Name);
            var newMatches = Matches.Select(match => match.Apply(replacements)).ToImmutableList();
            var newProjection = Projection.Apply(replacements);
            return new Specification(ImmutableList<Label>.Empty, newMatches, newProjection);
        }

        public ImmutableList<Product> Execute(ImmutableList<FactReference> startReferences, FactGraph graph)
        {
            var start = Given.Zip(startReferences, (given, reference) =>
                (name: given.Name, reference)
            ).ToImmutableDictionary(pair => pair.name, pair => pair.reference);
            ImmutableList<ImmutableDictionary<string, FactReference>> tuples = ExecuteMatches(start, Matches, graph);
            ImmutableList<Product> products = tuples.Select(tuple => CreateProduct(tuple, Projection)).ToImmutableList();
            return products;
        }

        public ImmutableList<Inverse> ComputeInverses()
        {
            throw new NotImplementedException();
        }

        public string ToDescriptiveString(int depth = 0)
        {
            var indent = new string(' ', depth * 4);
            var given = string.Join(", ", this.Given.Select(g => $"{g.Name}: {g.Type}"));
            var matches = string.Join("", this.Matches.Select(m => m.ToDescriptiveString(depth + 1)));
            var projection = this.Projection == null ? "" : " => " + this.Projection.ToDescriptiveString(depth);
            return $"{indent}({given}) {{\n{matches}{indent}}}{projection}\n";
        }

        private static ImmutableList<ImmutableDictionary<string, FactReference>> ExecuteMatches(ImmutableDictionary<string, FactReference> start, ImmutableList<Match> matches, FactGraph graph)
        {
            return matches.Aggregate(
                ImmutableList.Create(start),
                (set, match) => set
                    .SelectMany(references => ExecuteMatch(references, match, graph))
                    .ToImmutableList());
        }

        private static Product CreateProduct(ImmutableDictionary<string, FactReference> tuple, Projection projection)
        {
            var product = tuple.Keys.Aggregate(
                Product.Empty,
                (product, name) => product.With(name, new SimpleElement(tuple[name]))
            );
            product = ExecuteProjection(tuple, product, projection);
            return product;
        }

        private static Product ExecuteProjection(ImmutableDictionary<string, FactReference> tuple, Product product, Projection projection)
        {
            if (projection is SimpleProjection simpleProjection)
            {
                return Product.Empty.With(simpleProjection.Tag, new SimpleElement(tuple[simpleProjection.Tag]));
            }
            else
            {
                throw new NotImplementedException();
            }
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
    public class SpecificationOld
    {
        public SpecificationOld(PipelineOld pipeline, Projection projection)
        {
            Pipeline = pipeline;
            Projection = projection;
        }

        public PipelineOld Pipeline { get; }
        public Projection Projection { get; }

        public ImmutableList<InverseOld> ComputeInverses()
        {
            return Inverter.InvertSpecification(this).ToImmutableList();
        }
    }
}
