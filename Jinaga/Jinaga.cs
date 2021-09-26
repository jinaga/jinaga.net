using Jinaga.Facts;
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
                var startReferences = ImmutableList<FactReference>.Empty.Add(startReference);
                var products = await factManager.Query(startReferences, specification, cancellationToken);
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
            var pipeline = specification.Pipeline;
            var projection = specification.Projection;
            var observation = new FunctionObservation<TProjection>(added);
            var observer = new Observer<TProjection>(specification, startReference, factManager, observation);
            factManager.AddObserver(observer);
            observer.Start();
            return observer;
        }
    }
}
