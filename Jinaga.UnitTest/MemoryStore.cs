using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.UnitTest
{
    public class MemoryStore : IStore
    {
        private ImmutableDictionary<FactReference, Fact> factsByReference = ImmutableDictionary<FactReference, Fact>.Empty;
        private ImmutableList<Edge> edges = ImmutableList<Edge>.Empty;
        private ImmutableDictionary<FactReference, ImmutableList<FactReference>> ancestors = ImmutableDictionary<FactReference, ImmutableList<FactReference>>.Empty;

        public Task<ImmutableList<Fact>> Save(FactGraph graph, CancellationToken cancellationToken)
        {
            var newFacts = graph.FactReferences
                .Where(reference => !factsByReference.ContainsKey(reference))
                .Select(reference => graph.GetFact(reference))
                .ToImmutableList();
            factsByReference = factsByReference.AddRange(newFacts
                .Select(fact => new KeyValuePair<FactReference, Fact>(fact.Reference, fact))
            );
            var newPredecessors = newFacts
                .Select(fact => (
                    factReference: fact.Reference,
                    edges: fact.Predecessors
                        .SelectMany(predecessor => CreateEdges(fact, predecessor))
                        .ToImmutableList()
                ))
                .ToImmutableList();
            edges = edges.AddRange(newPredecessors
                .SelectMany(pair => pair.edges)
            );
            foreach (var (factReference, edges) in newPredecessors)
            {
                ancestors = ancestors.Add(
                    factReference,
                    edges
                        .SelectMany(edge => ancestors[edge.Predecessor])
                        .Append(factReference)
                        .Distinct()
                        .ToImmutableList()
                );
            }
            return Task.FromResult(newFacts);
        }

        public Task<ImmutableList<Product>> Query(ImmutableList<FactReference> startReferences, SpecificationOld specification, CancellationToken cancellationToken)
        {
            var products = ExecuteNestedPipeline(startReferences, specification.Pipeline, specification.Projection);
            return Task.FromResult(products);
        }

        public Task<ImmutableList<Product>> Query(ImmutableList<FactReference> startReferences, Specification specification, CancellationToken cancellationToken)
        {
            var start = specification.Given.Zip(startReferences, (given, reference) =>
                (name: given.Name, reference)
            ).ToImmutableDictionary(pair => pair.name, pair => pair.reference);
            var products = ExecuteNestedSpecification(start, specification);
            return Task.FromResult(products);
        }

        private ImmutableList<Product> ExecuteNestedSpecification(ImmutableDictionary<string, FactReference> start, Specification specification)
        {
            var givenProduct = start.Keys
                .Aggregate(Product.Empty, (product, name) =>
                    product.With(name, new SimpleElement(start[name]))
                );
            var tuples = specification.Matches.Aggregate(
                ImmutableList.Create(start),
                (set, match) => set
                    .SelectMany(references => ExecuteMatch(references, match))
                    .ToImmutableList());
            var products = tuples.Select(tuple =>
                tuple.Aggregate(givenProduct, (product, pair) =>
                    product.With(pair.Key, new SimpleElement(pair.Value))
                )
            ).ToImmutableList();
            return products;
        }

        private ImmutableList<ImmutableDictionary<string, FactReference>> ExecuteMatch(ImmutableDictionary<string, FactReference> references, Match match)
        {
            var condition = match.Conditions.Single();
            if (condition is PathCondition pathCondition)
            {
                var result = ExecutePathCondition(references, match.Unknown, pathCondition);
                var resultReferences = result.Select(reference =>
                    references.Add(match.Unknown.Name, reference));
                return resultReferences.ToImmutableList();
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private ImmutableList<FactReference> ExecutePathCondition(ImmutableDictionary<string, FactReference> start, Label unknown, PathCondition pathCondition)
        {
            var startingFactReference = start[pathCondition.LabelRight];
            var set = new FactReference[] { startingFactReference }.ToImmutableList();
            foreach (var role in pathCondition.RolesRight)
            {
                set = ExecutePredecessorStep(set, role.Name, role.TargetType);
            }
            foreach (var role in EnumerateRoles(pathCondition.RolesLeft, unknown.Type).Reverse())
            {
                set = ExecuteSuccessorStep(set, role.name, role.declaringType);
            }
            return set;
        }

        private IEnumerable<(string name, string declaringType)> EnumerateRoles(IEnumerable<Role> roles, string startingType)
        {
            var type = startingType;
            foreach (var role in roles)
            {
                yield return (role.Name, type);
                type = role.TargetType;
            }
        }

        private ImmutableList<Product> ExecuteNestedPipeline(ImmutableList<FactReference> startReferences, PipelineOld pipeline, Projection projection)
        {
            var subset = Subset.FromPipeline(pipeline);
            var namedSpecifications = projection.GetNamedSpecifications();
            var products = (
                from startReference in startReferences
                from product in ExecutePipeline(startReference, pipeline)
                select product).ToImmutableList();
            var collections =
                from namedSpecification in namedSpecifications
                let name = namedSpecification.name
                let childPipeline = pipeline.Compose(namedSpecification.specification.Pipeline)
                let childProducts = ExecuteNestedPipeline(startReferences, childPipeline, namedSpecification.specification.Projection)
                select (name, childProducts);
            var mergedProducts = collections
                .Aggregate(products, (source, collection) => MergeProducts(subset, collection.name, collection.childProducts, source));
            return mergedProducts;
        }

        private ImmutableList<Product> MergeProducts(Subset subset, string name, ImmutableList<Product> childProducts, ImmutableList<Product> products)
        {
            var mergedProducts =
                from product in products
                let matchingChildProducts = (
                    from childProduct in childProducts
                    where subset.Of(childProduct).Equals(subset.Of(product))
                    select childProduct
                ).ToImmutableList()
                select product.With(name, new CollectionElement(matchingChildProducts));
            return mergedProducts.ToImmutableList();
        }

        public Task<FactGraph> Load(ImmutableList<FactReference> references, CancellationToken cancellationToken)
        {
            var graph = references
                .SelectMany(reference => ancestors[reference])
                .Distinct()
                .Select(reference => factsByReference[reference])
                .Aggregate(FactGraph.Empty, (graph, fact) => graph.Add(fact));
            return Task.FromResult(graph);
        }

        private IEnumerable<Edge> CreateEdges(Fact successor, Predecessor predecessor)
        {
            switch (predecessor)
            {
                case PredecessorSingle single:
                    return new Edge[]
                    {
                        new Edge(single.Reference, single.Role, successor.Reference)
                    };
                case PredecessorMultiple multiple:
                    return multiple.References.Select(reference => new Edge(
                        reference, multiple.Role, successor.Reference
                    ));
                default:
                    throw new NotImplementedException();
            }
        }

        private ImmutableList<Product> ExecutePipeline(FactReference startReference, PipelineOld pipeline)
        {
            var initialTag = pipeline.Starts.Single().Name;
            var startingProducts = new Product[]
            {
                Product.Empty.With(initialTag, new SimpleElement(startReference))
            }.ToImmutableList();
            return pipeline.Paths.Aggregate(
                startingProducts,
                (products, path) => ExecutePath(products, path, pipeline)
            );
        }

        private ImmutableList<Product> ExecutePath(ImmutableList<Product> products, Path path, PipelineOld pipeline)
        {
            var results = products
                .SelectMany(product =>
                    ExecuteSteps(product.GetElement(path.Start.Name), path)
                        .Select(factReference => product.With(path.Target.Name, new SimpleElement(factReference))))
                .ToImmutableList();
            var conditionals = pipeline
                .Conditionals
                .Where(conditional => conditional.Start == path.Target);
            return results
                .Where(result => !conditionals.Any(conditional => !ConditionIsTrue(
                    result.GetElement(conditional.Start.Name),
                    conditional.ChildPipeline,
                    conditional.Exists)))
                .ToImmutableList();
        }

        private ImmutableList<FactReference> ExecuteSteps(Element element, Path path)
        {
            if (element is SimpleElement simple)
            {
                return ExecuteSteps(simple.FactReference, path);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private ImmutableList<FactReference> ExecuteSteps(FactReference startingFactReference, Path path)
        {
            var startingSet = new FactReference[] { startingFactReference }.ToImmutableList();
            var afterPredecessors = path.PredecessorSteps
                .Aggregate(startingSet, (set, predecessorStep) => ExecutePredecessorStep(
                    set, predecessorStep.Role, predecessorStep.TargetType
                ));
            var afterSuccessors = path.SuccessorSteps
                .Aggregate(afterPredecessors, (set, successorStep) => ExecuteSuccessorStep(
                    set, successorStep.Role, successorStep.TargetType
                ));
            return afterSuccessors;
        }

        private ImmutableList<FactReference> ExecutePredecessorStep(ImmutableList<FactReference> set, string role, string targetType)
        {
            return set
                .SelectMany(factReference => edges
                    .Where(edge =>
                        edge.Successor == factReference &&
                        edge.Role == role &&
                        edge.Predecessor.Type == targetType
                    )
                    .Select(edge => edge.Predecessor)
                )
                .ToImmutableList();
        }

        private ImmutableList<FactReference> ExecuteSuccessorStep(ImmutableList<FactReference> set, string role, string targetType)
        {
            return set
                .SelectMany(factReference => edges
                    .Where(edge =>
                        edge.Predecessor == factReference &&
                        edge.Role == role &&
                        edge.Successor.Type == targetType
                    )
                    .Select(edge => edge.Successor)
                )
                .ToImmutableList();
        }

        private bool ConditionIsTrue(Element element, PipelineOld pipeline, bool exists)
        {
            if (element is SimpleElement simple)
            {
                return ConditionIsTrue(simple.FactReference, pipeline, exists);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private bool ConditionIsTrue(FactReference factReference, PipelineOld pipeline, bool wantAny)
        {
            var hasAny = ExecutePipeline(factReference, pipeline).Any();
            return wantAny && hasAny || !wantAny && !hasAny;
        }
    }
}