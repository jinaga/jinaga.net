using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Observers;
using Jinaga.Products;
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
        private readonly NetworkManager networkManager;

        public Jinaga(IStore store, INetwork network)
        {
            networkManager = new NetworkManager(network, store, async (graph, added, cancellationToken) =>
            {
                if (factManager != null)
                {
                    await factManager.NotifyObservers(graph, added, cancellationToken);
                }
            });
            factManager = new FactManager(store, networkManager);
        }

        public async Task<TFact> Fact<TFact>(TFact prototype) where TFact: class
        {
            if (prototype == null)
            {
                throw new ArgumentNullException(nameof(prototype));
            }

            var graph = factManager.Serialize(prototype);
            using (var source = new CancellationTokenSource())
            {
                var token = source.Token;
                await factManager.Save(graph, token);
            }

            return factManager.Deserialize<TFact>(graph, graph.Last);
        }

        public async Task<ImmutableList<TProjection>> Query<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            CancellationToken cancellationToken = default) where TFact : class
        {
            if (given == null)
            {
                throw new ArgumentNullException(nameof(given));
            }

            var graph = factManager.Serialize(given);
            var givenReference = graph.Last;
            var givenReferences = ImmutableList.Create(givenReference);
            if (specification.CanRunOnGraph)
            {
                var products = specification.Execute(givenReferences, graph);
                var productAnchorProjections = factManager.DeserializeProductsFromGraph(
                    graph, specification.Projection, products, typeof(TProjection), "", null);
                return productAnchorProjections.Select(pap => (TProjection)pap.Projection).ToImmutableList();
            }
            else
            {
                await factManager.Fetch(givenReferences, specification, cancellationToken);
                var products = await factManager.Query(givenReferences, specification, cancellationToken);
                var productProjections = await factManager.ComputeProjections(specification.Projection, products, typeof(TProjection), null, string.Empty, cancellationToken);
                var projections = productProjections
                    .Select(pair => (TProjection)pair.Projection)
                    .ToImmutableList();
                return projections;
            }
        }

        public IWatch Watch<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Action<TProjection> added)
            where TFact : class
        {
            return Watch<TFact, TProjection>(specification, given,
                projection =>
                {
                    added(projection);
                    Func<Task> result = () => Task.CompletedTask;
                    return Task.FromResult(result);
                }
            );
        }

        public IWatch Watch<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Func<TProjection, Action> added)
            where TFact : class
        {
            return Watch<TFact, TProjection>(specification, given,
                projection =>
                {
                    var removed = added(projection);
                    Func<Task> result = () =>
                    {
                        removed();
                        return Task.CompletedTask;
                    };
                    return Task.FromResult(result);
                }
            );
        }

        public IWatch Watch<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Func<TProjection, Task> added)
            where TFact: class
        {
            return Watch<TFact, TProjection>(specification, given,
                async projection =>
                {
                    await added(projection);
                    return () => Task.CompletedTask;
                }
            );
        }

        public IWatch Watch<TFact, TProjection>(
            Specification<TFact, TProjection> specification,
            TFact given,
            Func<TProjection, Task<Func<Task>>> added)
            where TFact : class
        {
            if (given == null)
            {
                throw new ArgumentNullException(nameof(given));
            }

            var graph = factManager.Serialize(given);
            Func<object, Task<Func<Task>>> onAdded = (object obj) => added((TProjection)obj);
            var observer = factManager.StartObserver(graph, specification, typeof(TProjection), onAdded);
            return observer;
        }
    }
}
