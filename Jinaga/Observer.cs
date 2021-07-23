﻿using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Observers;
using Jinaga.Pipelines2;
using Jinaga.Projections;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
    public class Observer<TProjection> : IObserver
    {
        private readonly Pipeline pipeline;
        private readonly Projection projection;
        private readonly FactReference startReference;
        private readonly FactManager factManager;
        private readonly Observation<TProjection> observation;
        private readonly ImmutableList<Inverse> inverses;

        private Task? initialize;
        private CancellationTokenSource cancelInitialize = new CancellationTokenSource();

        public Task Initialized => initialize!;

        internal Observer(Pipeline pipeline, Projection projection, FactReference startReference, FactManager factManager, Observation<TProjection> observation)
        {
            this.pipeline = pipeline;
            this.projection = projection;
            this.startReference = startReference;
            this.factManager = factManager;
            this.observation = observation;
            this.inverses = pipeline.ComputeInverses();
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
            var products = await factManager.Query(startReference, pipeline, cancellationToken);
            var results = await factManager.ComputeProjections<TProjection>(projection, products, cancellationToken);
            await observation.NotifyAdded(results);
        }

        public async Task FactsAdded(ImmutableList<Fact> added, FactGraph graph, CancellationToken cancellationToken)
        {
            var resultsAdded = ImmutableList<TProjection>.Empty;
            var startReferences = added.Select(a => a.Reference).ToImmutableList();
            foreach (var inverse in inverses)
            {
                var productsAdded = ImmutableList<Product>.Empty;
                var inversePipeline = inverse.InversePipeline;
                var matchingReferences = startReferences
                    .Where(r => inversePipeline.Starts.Any(start => r.Type == start.Type))
                    .ToImmutableList();
                if (matchingReferences.Any())
                {
                    var products = await factManager.QueryAll(
                        startReferences,
                        inversePipeline,
                        cancellationToken);
                    foreach (var product in products)
                    {
                        var affected = product.GetFactReference(inverse.AffectedTag);
                        if (affected == startReference)
                        {
                            productsAdded = productsAdded.Add(product);
                        }
                    }
                }
                if (productsAdded.Any())
                {
                    var results = await factManager.ComputeProjections<TProjection>(projection, productsAdded, cancellationToken);
                    resultsAdded = resultsAdded.AddRange(results);
                }
            }
            if (resultsAdded.Any())
            {
                await observation.NotifyAdded(resultsAdded);
            }
        }
    }
}
