using Jinaga.Facts;
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
        private readonly Action<LoadBatch> batchFinished;
        
        private ImmutableHashSet<FactReference> factReferences =
            ImmutableHashSet<FactReference>.Empty;

        public LoadBatch(INetwork network, IStore store, Func<FactGraph, ImmutableList<Fact>, CancellationToken, Task> notifyObservers, Action<LoadBatch> batchFinished)
        {
            this.network = network;
            this.store = store;
            this.notifyObservers = notifyObservers;
            this.batchFinished = batchFinished;
        }

        public Task Completed => Task.CompletedTask;

        public void Add(ImmutableList<FactReference> unknownFactReferences)
        {
            factReferences = factReferences.Union(unknownFactReferences);
        }

        public void Trigger()
        {
            throw new NotImplementedException();
        }

        private async Task Load(CancellationToken cancellationToken)
        {
            var graph = await network.Load(factReferences.ToImmutableList(), cancellationToken);

            // Save the facts.
            var added = await store.Save(graph, cancellationToken);

            // Notify observers.
            await notifyObservers(graph, added, cancellationToken);
        }
    }
}