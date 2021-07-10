using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Services;

namespace Jinaga.UnitTest
{
    public class MemoryStore : IStore
    {
        private ImmutableDictionary<FactReference, Fact> factsByReference = ImmutableDictionary<FactReference, Fact>.Empty;
        private ImmutableList<Edge> edges = ImmutableList<Edge>.Empty;
        private ImmutableDictionary<FactReference, ImmutableList<FactReference>> ancestors = ImmutableDictionary<FactReference, ImmutableList<FactReference>>.Empty;

        public Task<ImmutableList<Fact>> Save(FactGraph graph)
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

        public Task<ImmutableList<Product>> Query(FactReference startReference, string initialTag, ImmutableList<Path> paths)
        {
            var startingProducts = new Product[]
            {
                Product.Init(initialTag, startReference)
            }.ToImmutableList();
            var products = paths.Aggregate(
                startingProducts,
                (products, path) => ExecutePath(products, path)
            );
            return Task.FromResult(products);
        }

        public Task<FactGraph> Load(ImmutableList<FactReference> references)
        {
            var graph = references
                .SelectMany(reference => ancestors[reference])
                .Distinct()
                .Select(reference => factsByReference[reference])
                .Aggregate(new FactGraph(), (graph, fact) => graph.Add(fact));
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
                default:
                    throw new NotImplementedException();
            }
        }

        private ImmutableList<Product> ExecutePath(ImmutableList<Product> products, Path path)
        {
            return products
                .SelectMany(product => ExecuteSteps(product.GetFactReference(path.StartingTag), path.Steps)
                    .Select(factReference => product.With(path.Tag, factReference))
                )
                .ToImmutableList();
        }

        private ImmutableList<FactReference> ExecuteSteps(FactReference startingFactReference, ImmutableList<Step> steps)
        {
            var startingSet = new FactReference[] { startingFactReference }.ToImmutableList();
            return steps.Aggregate(startingSet, (set, step) => ExecuteStep(set, step));
        }

        private ImmutableList<FactReference> ExecuteStep(ImmutableList<FactReference> set, Step step)
        {
            switch (step)
            {
                case PredecessorStep predecessor:
                    return ExecutePredecessorStep(set, predecessor.Role, predecessor.TargetType);
                default:
                    throw new NotImplementedException();
            }
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
    }
}