using System;
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
        private ImmutableList<Fact> facts = ImmutableList<Fact>.Empty;

        public Task<ImmutableList<Fact>> Save(ImmutableList<Fact> newFacts)
        {
            facts = facts.AddRange(newFacts);
            return Task.FromResult(newFacts);
        }

        public Task<ImmutableList<Product>> Query(FactReference startReference, string initialTag, ImmutableList<Path> paths)
        {
            var startingProducts = facts
                .Where(fact => fact.Reference == startReference)
                .Select(fact => Product.Init(initialTag, fact))
                .ToImmutableList();
            var products = paths.Aggregate(startingProducts, (products, path) => ExecutePath(products, path));
            return Task.FromResult(products);
        }

        public Task<ImmutableList<Fact>> Load(ImmutableList<FactReference> references)
        {
            throw new NotImplementedException();
        }

        private ImmutableList<Product> ExecutePath(ImmutableList<Product> products, Path path)
        {
            return products
                .SelectMany(product => ExecuteSteps(product.GetFact(path.StartingTag), path.Steps)
                    .Select(fact => product.With(path.Tag, fact))
                )
                .ToImmutableList();
        }

        private ImmutableList<Fact> ExecuteSteps(Fact startingFact, ImmutableList<Step> steps)
        {
            var startingSet = new Fact[] { startingFact }.ToImmutableList();
            return steps.Aggregate(startingSet, (set, step) => ExecuteStep(set, step));
        }

        private ImmutableList<Fact> ExecuteStep(ImmutableList<Fact> set, Step step)
        {
            switch (step)
            {
                case PredecessorStep predecessor:
                    return ExecutePredecessorStep(set, predecessor.Role, predecessor.TargetType);
                default:
                    throw new NotImplementedException();
            }
        }

        private ImmutableList<Fact> ExecutePredecessorStep(ImmutableList<Fact> set, string role, string targetType)
        {
            return set
                .SelectMany(fact => fact.Predecessors
                .Where(predecessor => predecessor.Role == role))
                .SelectMany(predecessor =>
                {
                    switch (predecessor)
                    {
                        case PredecessorSingle single:
                            return new FactReference[] { single.Reference };
                        default:
                            throw new NotImplementedException();
                    }
                })
                .Where(reference => reference.Type == targetType)
                .SelectMany(reference => facts.Where(fact => fact.Reference == reference))
                .ToImmutableList();
        }

        private ImmutableList<Fact> ExecuteSuccessorStep(ImmutableList<Fact> set, string role, string targetType)
        {
            return set
                .SelectMany(startingFact => facts
                    .Where(fact => fact.Reference.Type == targetType)
                    .Where(fact => fact.Predecessors
                        .Any(predecessor =>
                            predecessor.Role == role &&
                            PredecessorIncludes(predecessor, startingFact.Reference)
                        )
                    )
                )
                .ToImmutableList();
        }

        private bool PredecessorIncludes(Predecessor predecessor, FactReference reference)
        {
            switch (predecessor)
            {
                case PredecessorSingle single:
                    return single.Reference == reference;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}