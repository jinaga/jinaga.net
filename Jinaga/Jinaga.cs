using Jinaga.Managers;
using Jinaga.Observers;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
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
            var products = await factManager.Query(startReference, pipeline, cancellationToken);
            var results = await factManager.ComputeProjections<TProjection>(specification.Projection, products, cancellationToken);
            return results;
        }

        public Observer<TProjection> Watch<TFact, TProjection>(
            TFact start,
            Specification<TFact, TProjection> specification,
            Func<Observation<TProjection>, Observation<TProjection>> config)
        {
            if (start == null)
            {
                throw new ArgumentNullException(nameof(start));
            }

            var graph = factManager.Serialize(start);
            var startReference = graph.Last;
            var pipeline = specification.Pipeline;
            var observation = config(new Observation<TProjection>());
            throw new NotImplementedException();
            // var observer = new Observer<TProjection>(pipeline, startReference, factManager, observation);
            // factManager.AddObserver(observer);
            // observer.Start();
            // return observer;
        }
    }
}
