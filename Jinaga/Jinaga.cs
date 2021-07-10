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
            var graph = FactSerializer.Serialize(prototype);
            var added = await store.Save(graph);
            var reference = graph.Last;
            return FactSerializer.Deserialize<T>(graph, reference);
        }

        public async Task<ImmutableList<TProjection>> Query<TFact, TProjection>(TFact start, Specification<TFact, TProjection> specification)
        {
            var startReference = FactSerializer.Serialize(start).Last;
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
                    var graph = await store.Load(references);
                    var projections = references
                        .Select(reference => FactSerializer.Deserialize<TProjection>(graph, reference))
                        .ToImmutableList();
                    return projections;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
