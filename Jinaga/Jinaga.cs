using Jinaga.Managers;
using Jinaga.Observers;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
    public class Jinaga
    {
        private readonly FactManager factManager;

        public Jinaga(IStore store)
        {
            this.factManager = new FactManager(store);
        }

        public async Task<TFact> Fact<TFact>(TFact prototype) where TFact: class
        {
            if (prototype == null)
            {
                throw new ArgumentNullException(nameof(prototype));
            }

            var graph = factManager.Serialize(prototype);
            await factManager.Save(graph, default(CancellationToken));
            return factManager.Deserialize<TFact>(graph, graph.Last);
        }

        public async Task<ImmutableList<TProjection>> Query<TFact, TProjection>(
            TFact start,
            Specification<TFact, TProjection> specification,
            CancellationToken cancellationToken = default) where TFact : class
        {
            if (start == null)
            {
                throw new ArgumentNullException(nameof(start));
            }

            var graph = factManager.Serialize(start);
            var startReference = graph.Last;
            var pipeline = specification.Pipeline;
            if (pipeline.CanRunOnGraph)
            {
                var products = pipeline.Execute(startReference, graph);
                var projection = specification.Projection;
                return projection switch
                {
                    SimpleProjection simple => products
                        .Select(product => factManager.Deserialize<TProjection>(
                            graph, product.GetFactReference(simple.Tag)
                        ))
                        .ToImmutableList(),
                    _ => throw new NotImplementedException()
                };
            }
            else
            {
                var products = await factManager.Query(startReference, pipeline, cancellationToken);
                var productProjections = await factManager.ComputeProjections<TProjection>(specification.Projection, products, cancellationToken);
                var projections = productProjections
                    .Select(pair => pair.Projection)
                    .ToImmutableList();
                return projections;
            }
        }

        public Observer<TProjection> Watch<TFact, TProjection>(
            TFact start,
            Specification<TFact, TProjection> specification,
            Func<Observation<TProjection>, IObservation<TProjection>> config)
        {
            if (start == null)
            {
                throw new ArgumentNullException(nameof(start));
            }

            var graph = factManager.Serialize(start);
            var startReference = graph.Last;
            var pipeline = specification.Pipeline;
            var projection = specification.Projection;
            var observation = config(new Observation<TProjection>());
            var observer = new Observer<TProjection>(pipeline, projection, startReference, factManager, observation);
            factManager.AddObserver(observer);
            observer.Start();
            return observer;
        }
    }
}
