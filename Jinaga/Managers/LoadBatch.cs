﻿using Jinaga.Facts;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Managers
{
    class LoadBatch
    {
        private readonly INetwork network;
        private readonly IStore store;
        private readonly Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers;

        public Task Completed { get; }

        private ImmutableHashSet<FactReference>? factReferences =
            ImmutableHashSet<FactReference>.Empty;
        private readonly TaskCompletionSource<bool> start;

        public LoadBatch(INetwork network, IStore store, Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers, Action<LoadBatch> batchStarted)
        {
            this.network = network;
            this.store = store;
            this.notifyObservers = notifyObservers;

            start = new TaskCompletionSource<bool>();
            Completed = Task.Run(async () =>
            {
                await Task.WhenAny(start.Task, Task.Delay(100)).ConfigureAwait(false);
                batchStarted(this);
                using var cancellationTokenSource = new CancellationTokenSource();
                await Load(cancellationTokenSource.Token).ConfigureAwait(false);
            });
        }

        public void Add(ImmutableList<FactReference> unknownFactReferences)
        {
            lock (this)
            {
                if (factReferences == null)
                {
                    throw new InvalidOperationException("Cannot add fact references to a batch that has already started.");
                }
                factReferences = factReferences.Union(unknownFactReferences);
            }
        }

        public void Trigger()
        {
            start.SetResult(true);
        }

        private async Task Load(CancellationToken cancellationToken)
        {
            ImmutableHashSet<FactReference>? factReferences;
            lock (this)
            {
                factReferences = this.factReferences;
                this.factReferences = null;
            }
            if (factReferences == null)
            {
                throw new InvalidOperationException("Cannot start a batch multiple times.");
            }
            var graph = await network.Load(factReferences.ToImmutableList(), cancellationToken).ConfigureAwait(false);

            // Save the facts.
            var added = await store.Save(graph, false, cancellationToken).ConfigureAwait(false);

            // Notify observers.
            await notifyObservers(graph, added, cancellationToken).ConfigureAwait(false);
        }
    }
}