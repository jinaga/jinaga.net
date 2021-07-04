using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Services;

namespace Jinaga
{
    public class Jinaga
    {
        private readonly IStore store;

        public Jinaga(IStore store)
        {
            this.store = store;
        }

        public async Task<T> Fact<T>(T prototype)
        {
            var newFacts = FactSerializer.Serialize(prototype);
            var added = await store.Save(newFacts);
            return prototype;
        }

        public async Task<ImmutableList<TProjection>> Query<TFact, TProjection>(TFact start, Specification<TFact, TProjection> specification)
        {
            var startFact = FactSerializer.Serialize(start).Last();
            var startReference = startFact.Reference;
            var pipeline = specification.Compile();
            var products = await store.Query(startReference, pipeline.InitialTag, pipeline.Paths);
            var results = await ComputeProjections<TProjection>(pipeline.Projection, products);
            return results;
        }

        private async Task<ImmutableList<TProjection>> ComputeProjections<TProjection>(Projection projection, ImmutableList<Product> products)
        {
            switch (projection)
            {
                case SimpleProjection simple:
                    var references = products
                        .Select(product => product.GetFactReference(simple.Tag))
                        .ToImmutableList();
                    var facts = await store.Load(references);
                    var projections = facts
                        .Select(fact => FactSerializer.Deserialize<TProjection>(facts, fact.Reference))
                        .ToImmutableList();
                    return projections;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
