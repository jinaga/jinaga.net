using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Observers;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
    public class Observer<TProjection> : IObserver
    {
        private readonly Specification specification;
        private readonly FactReference startReference;
        private readonly FactManager factManager;
        private readonly IObservation observation;
        private readonly ImmutableList<Inverse> inverses;

        private Task? initialize;
        private CancellationTokenSource cancelInitialize = new CancellationTokenSource();

        private ImmutableDictionary<Product, Func<Task>> removalsByProduct =
            ImmutableDictionary<Product, Func<Task>>.Empty;

        public Task Initialized => initialize!;

        internal Observer(Specification specification, FactReference startReference, FactManager factManager, IObservation observation)
        {
            this.specification = specification;
            this.startReference = startReference;
            this.factManager = factManager;
            this.observation = observation;
            this.inverses = specification.ComputeInverses();
        }

        internal void Start()
        {
            var cancellationToken = cancelInitialize.Token;
            initialize = Task.Run(() => RunInitialQuery(cancellationToken), cancellationToken);
        }

        public async Task Stop()
        {
            if (initialize != null)
            {
                cancelInitialize.CancelAfter(TimeSpan.FromSeconds(2));
                await initialize;
            }
            factManager.RemoveObserver(this);
        }

        private async Task RunInitialQuery(CancellationToken cancellationToken)
        {
            var startReferences = ImmutableList<FactReference>.Empty.Add(startReference);
            var products = await factManager.Query(startReferences, specification, cancellationToken);
            var productProjections = await factManager.ComputeProjections(specification.Projection, products, typeof(TProjection), observation, cancellationToken);
            var removals = await observation.NotifyAdded(productProjections);
            lock (this)
            {
                removalsByProduct = removalsByProduct.AddRange(removals);
            }
        }

        public async Task FactsAdded(ImmutableList<Fact> added, FactGraph graph, CancellationToken cancellationToken)
        {
            if (initialize == null)
            {
                return;
            }
            await initialize;

            var productsAdded = ImmutableList<Product>.Empty;
            var productsRemoved = ImmutableList<Product>.Empty;
            var startReferences = added.Select(a => a.Reference).ToImmutableList();
            foreach (var inverse in inverses)
            {
                var inversePipeline = inverse.InversePipeline;
                var matchingReferences = startReferences
                    .Where(r => inversePipeline.Starts.Any(start => r.Type == start.Type))
                    .ToImmutableList();
                if (matchingReferences.Any())
                {
                    var products = inversePipeline.CanRunOnGraph
                        ? inversePipeline.Execute(startReferences, graph)
                        : await factManager.Query(
                            startReferences,
                            new Specification(inversePipeline, specification.Projection),
                            cancellationToken);
                    foreach (var product in products)
                    {
                        var identifyingProduct = inverse.Subset.Of(product);
                        var affected = identifyingProduct.GetElement(inverse.AffectedTag);
                        if (affected is SimpleElement simple && simple.FactReference == startReference)
                        {
                            if (inverse.Operation == Operation.Add)
                            {
                                productsAdded = productsAdded.Add(identifyingProduct);
                            }
                            else if (inverse.Operation == Operation.Remove)
                            {
                                productsRemoved = productsRemoved.Add(identifyingProduct);
                            }
                        }
                    }
                }
            }
            if (productsAdded.Any())
            {
                var addedGraph = await factManager.LoadProducts(productsAdded, cancellationToken);
                var productProjections = factManager.DeserializeProductsFromGraph(graph, specification.Projection, productsAdded, typeof(TProjection), observation);
                var removals = await observation.NotifyAdded(productProjections);
                lock (this)
                {
                    removalsByProduct = removalsByProduct.AddRange(removals);
                }
            }
            if (productsRemoved.Any())
            {
                var removals = productsRemoved
                    .Select(product => removalsByProduct.GetValueOrDefault(product)!)
                    .Where(identity => identity != null)
                    .ToImmutableList();
                foreach (var removal in removals)
                {
                    await removal();
                }
                lock (this)
                {
                    removalsByProduct = removalsByProduct.RemoveRange(productsRemoved);
                }
            }
        }
    }
}
