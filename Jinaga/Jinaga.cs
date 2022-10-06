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
            var startReferences = ImmutableList.Create(startReference);
            if (specification.CanRunOnGraph)
            {
                var products = specification.Execute(startReferences, graph);
                return specification.Projection switch
                {
                    SimpleProjection simple => products
                        .Select(product => factManager.Deserialize<TProjection>(
                            graph, product.GetElement(simple.Tag)
                        ))
                        .ToImmutableList(),
                    _ => throw new NotImplementedException()
                };
            }
            else
            {
                var products = await factManager.Query(startReferences, specification, cancellationToken);
                var productProjections = await factManager.ComputeProjections(specification.Projection, products, typeof(TProjection), null, Product.Empty, string.Empty, cancellationToken);
                var projections = productProjections
                    .Select(pair => (TProjection)pair.Projection)
                    .ToImmutableList();
                return projections;
            }
        }

        public Observer<TProjection> Watch<TFact, TProjection>(
            TFact start,
            Specification<TFact, TProjection> specification,
            Action<TProjection> added)
        {
            return Watch<TFact, TProjection>(start, specification,
                projection =>
                {
                    added(projection);
                    Func<Task> result = () => Task.CompletedTask;
                    return Task.FromResult(result);
                }
            );
        }

        public Observer<TProjection> Watch<TFact, TProjection>(
            TFact start,
            Specification<TFact, TProjection> specification,
            Func<TProjection, Action> added)
        {
            return Watch<TFact, TProjection>(start, specification,
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

        public Observer<TProjection> Watch<TFact, TProjection>(
            TFact start,
            Specification<TFact, TProjection> specification,
            Func<TProjection, Task> added)
        {
            return Watch<TFact, TProjection>(start, specification,
                async projection =>
                {
                    await added(projection);
                    return () => Task.CompletedTask;
                }
            );
        }

        public Observer<TProjection> Watch<TFact, TProjection>(
            TFact start,
            Specification<TFact, TProjection> specification,
            Func<TProjection, Task<Func<Task>>> added)
        {
            if (start == null)
            {
                throw new ArgumentNullException(nameof(start));
            }

            var graph = factManager.Serialize(start);
            var startReference = graph.Last;
            var projection = specification.Projection;
            var observation = new FunctionObservation<TProjection>(added);
            var observer = new Observer<TProjection>(specification, Product.Empty.With(specification.Given.First().Name, new SimpleElement(startReference)), factManager, observation);
            factManager.AddObserver(observer);
            observer.Start();
            return observer;
        }
    }
}
