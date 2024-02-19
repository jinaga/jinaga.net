using Jinaga.Facts;
using Jinaga.Identity;
using Jinaga.Managers;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    class Observer : IObserver, IWatchContext
    {
        private readonly Specification specification;
        private readonly string specificationHash;
        private readonly FactReferenceTuple givenTuple;
        private readonly FactManager factManager;

        private CancellationTokenSource cancelInitialize = new CancellationTokenSource();

        private SynchronizationContext? synchronizationContext;
        private Task<bool>? cachedTask;
        private Task? loadedTask;

        private ImmutableList<SpecificationListener> listeners =
            ImmutableList<SpecificationListener>.Empty;
        private ImmutableDictionary<FactReferenceTuple, Func<Task>> removalsByProduct =
            ImmutableDictionary<FactReferenceTuple, Func<Task>>.Empty;
        private ImmutableList<AddedHandler> addedHandlers =
            ImmutableList<AddedHandler>.Empty;
        private ImmutableHashSet<FactReferenceTuple> notifiedTuples =
            ImmutableHashSet<FactReferenceTuple>.Empty;
        private ImmutableList<string> feeds = ImmutableList<string>.Empty;

        internal Observer(Specification specification, FactReferenceTuple givenTuple, FactManager factManager, Func<object, Task<Func<Task>>> onAdded)
        {
            this.specification = specification;
            this.givenTuple = givenTuple;
            this.factManager = factManager;

            // Add the initial handler.
            addedHandlers = addedHandlers.Add(new AddedHandler(givenTuple, "", onAdded));

            // Identify a specification by its hash.
            specificationHash = IdentityUtilities.ComputeSpecificationHash(specification, givenTuple);
        }

        public Task<bool> Cached => cachedTask ?? Task.FromResult(false);
        public Task Loaded => loadedTask ?? Task.CompletedTask;

        internal void Start(bool keepAlive)
        {
            // Capture the synchronization context so that notifications
            // can be executed on the same thread.
            synchronizationContext = SynchronizationContext.Current;

            var cancellationToken = cancelInitialize.Token;
            cachedTask = Task.Run(async () =>
                await ReadFromStore(cancellationToken).ConfigureAwait(false));
            loadedTask = Task.Run(async () =>
            {
                bool cached = await cachedTask.ConfigureAwait(false);
                await FetchFromNetwork(cached, keepAlive, cancellationToken).ConfigureAwait(false);
            });
        }

        public async Task Refresh(CancellationToken? cancellationToken = null)
        {
            if (!loadedTask?.IsCompleted ?? false)
            {
                return;
            }
            
            if (cancellationToken != null)
            {
                await FetchFromNetwork(true, false, cancellationToken.Value).ConfigureAwait(false);
            }
            else
            {
                using var source = new CancellationTokenSource();
                await FetchFromNetwork(true, false, source.Token).ConfigureAwait(false);
            }
        }

        private async Task<bool> ReadFromStore(CancellationToken cancellationToken)
        {
            // Always read from local store first.
            // This has the positive effect of presenting local data quickly
            // even when the specification is different.

            // Perhaps we can search the MRU for related specifications.
            // Or perhaps we can apply a timeout downstream to load from
            // local storage after expired or error.

            //DateTime? mruDate = await factManager.GetMruDate(specificationHash).ConfigureAwait(false);
            //if (mruDate == null)
            //{
            //    return false;
            //}

            // Read from local storage.
            await Read(cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task FetchFromNetwork(bool cached, bool keepAlive, CancellationToken cancellationToken)
        {
            if (!cached)
            {
                // Fetch from the network first,
                // then read from local storage.
                await Fetch(cancellationToken, keepAlive).ConfigureAwait(false);
                await Read(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Already read from local storage.
                // Fetch from the network to update the cache.
                await Fetch(cancellationToken, keepAlive).ConfigureAwait(false);
            }
            await factManager.SetMruDate(specificationHash, DateTime.UtcNow).ConfigureAwait(false);
        }

        public void OnAdded(FactReferenceTuple anchor, string path, Func<object, Task<Func<Task>>> added)
        {
            lock (this)
            {
                addedHandlers = addedHandlers.Add(new AddedHandler(anchor, path, added));
            }
        }

        public void Stop()
        {
            cancelInitialize.Cancel();
            foreach (var listener in listeners)
            {
                factManager.RemoveSpecificationListener(listener);
            }
            cancelInitialize.Dispose();
            factManager.Unsubscribe(feeds);
            feeds = ImmutableList<string>.Empty;
        }

        private async Task Read(CancellationToken cancellationToken)
        {
            var results = await factManager.Read(givenTuple, specification, specification.Projection.Type, this, cancellationToken).ConfigureAwait(false);
            AddSpecificationListeners();
            var givenSubset = specification.Givens
                .Select(g => g.Label.Name)
                .Aggregate(Subset.Empty, (subset, name) => subset.Add(name));

            await SynchronizeNotifyAdded(results, givenSubset).ConfigureAwait(false);
        }

        private async Task Fetch(CancellationToken cancellationToken, bool keepAlive)
        {
            if (keepAlive)
            {
                var feeds = await factManager.Subscribe(givenTuple, specification, cancellationToken);
                lock (this)
                {
                    this.feeds = feeds;
                }
            }
            else
            {
                await factManager.Fetch(givenTuple, specification, cancellationToken);
            }
        }

        private void AddSpecificationListeners()
        {
            var inverses = specification.ComputeInverses();
            ImmutableList<SpecificationListener> listeners = inverses.Select(inverse => factManager.AddSpecificationListener(
                inverse.InverseSpecification,
                async (ImmutableList<Product> results, CancellationToken cancellationToken) => await OnResult(inverse, results, cancellationToken).ConfigureAwait(false)
            )).ToImmutableList();
            this.listeners = listeners;
        }

        private async Task OnResult(Inverse inverse, ImmutableList<Product> products, CancellationToken cancellationToken)
        {
            // Filter out results that do not match the given.
            var givenSubset = inverse.GivenSubset;
            var matchingProducts = products
                .Where(product => givenSubset.Of(product).Equals(givenTuple))
                .ToImmutableList();
            if (matchingProducts.IsEmpty)
            {
                return;
            }

            if (inverse.Operation == InverseOperation.Add || inverse.Operation == InverseOperation.MaybeAdd)
            {
                Projection projection = inverse.InverseSpecification.Projection;
                var results = await factManager.ComputeProjections(projection, matchingProducts, projection.Type, this, inverse.Path, cancellationToken).ConfigureAwait(false);
                await SynchronizeNotifyAdded(results, inverse.ParentSubset).ConfigureAwait(false);
            }
            else if (inverse.Operation == InverseOperation.Remove || inverse.Operation == InverseOperation.MaybeRemove)
            {
                await SynchronizeNotifyRemoved(inverse, matchingProducts).ConfigureAwait(false);
            }
        }

        private async Task SynchronizeNotifyAdded(ImmutableList<ProjectedResult> results, Subset givenSubset)
        {
            await SynchronizeOperaton(() => NotifyAdded(results, givenSubset)).ConfigureAwait(false);
        }

        private async Task SynchronizeNotifyRemoved(Inverse inverse, ImmutableList<Product> matchingProducts)
        {
            await SynchronizeOperaton(() => NotifyRemoved(matchingProducts, inverse.ResultSubset)).ConfigureAwait(false);
        }

        private async Task SynchronizeOperaton(Func<Task> operation)
        {
            if (synchronizationContext == null)
            {
                await operation().ConfigureAwait(false);
            }
            else
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                synchronizationContext.Post(async _ =>
                {
                    try
                    {
                        await operation().ConfigureAwait(false);
                        taskCompletionSource.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        taskCompletionSource.SetException(ex);
                    }
                }, null);
                await taskCompletionSource.Task.ConfigureAwait(false);
            }
        }

        private async Task NotifyAdded(ImmutableList<ProjectedResult> results, Subset parentSubset)
        {
            foreach (var result in results)
            {
                var parentTuple = parentSubset.Of(result.Product);
                var matchingAddedHandlers = addedHandlers
                    .Where(hander => hander.Anchor.Equals(parentTuple) && hander.Path == result.Path);
                foreach (var addedHandler in matchingAddedHandlers)
                {
                    var resultAdded = addedHandler.Added;
                    // Don't call result added if we have already called it for this tuple.
                    var resultTuple = result.Product.GetAnchor();
                    if (FirstTimeNotified(resultTuple))
                    {
                        var removal = await resultAdded(result.Projection).ConfigureAwait(false);
                        lock (this)
                        {
                            removalsByProduct = removalsByProduct.Add(resultTuple, removal);
                        }
                    }
                }

                // Recursively notify added for specification results.
                if (result.Collections.Any())
                {
                    var subset = result.Product.Names
                        .Where(name => result.Product.GetElement(name) is SimpleElement)
                        .Aggregate(
                            Subset.Empty,
                            (sub, name) => sub.Add(name)
                        );
                    foreach (var collection in result.Collections)
                    {
                        await NotifyAdded(collection.Results, subset).ConfigureAwait(false);
                    }
                }
            }
        }

        private bool FirstTimeNotified(FactReferenceTuple resultTuple)
        {
            lock (this)
            {
                if (!notifiedTuples.Contains(resultTuple))
                {
                    notifiedTuples = notifiedTuples.Add(resultTuple);
                    return true;
                }

                return false;
            }
        }

        private async Task NotifyRemoved(ImmutableList<Product> products, Subset resultSubset)
        {
            foreach (var product in products)
            {
                var resultTuple = resultSubset.Of(product);
                if (removalsByProduct.TryGetValue(resultTuple, out var removal))
                {
                    await removal().ConfigureAwait(false);
                    lock (this)
                    {
                        removalsByProduct = removalsByProduct.Remove(resultTuple);
                    }
                }
            }
        }
    }
}
