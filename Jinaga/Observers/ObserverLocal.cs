using Jinaga.Facts;
using Jinaga.Identity;
using Jinaga.Managers;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    class ObserverLocal : IObserver, IWatchContext
    {
        protected readonly Specification specification;
        protected readonly string specificationHash;
        protected readonly FactReferenceTuple givenTuple;
        protected readonly FactManager factManager;
        protected readonly ILogger logger;

        protected CancellationTokenSource cancelInitialize = new CancellationTokenSource();

        protected SynchronizationContext? synchronizationContext;
        protected Task<bool>? cachedTask;
        protected Task? loadedTask;

        private ImmutableList<SpecificationListener> listeners =
            ImmutableList<SpecificationListener>.Empty;
        private ImmutableList<AddedHandler> addedHandlers =
            ImmutableList<AddedHandler>.Empty;
        private ImmutableDictionary<FactReferenceTuple, RowState> rowStates =
            ImmutableDictionary<FactReferenceTuple, RowState>.Empty;
        protected ImmutableList<string> feeds = ImmutableList<string>.Empty;

        internal ObserverLocal(Specification specification, FactReferenceTuple givenTuple, FactManager factManager, Func<object, Task<Func<Task>>> onAdded, ILoggerFactory loggerFactory)
        {
            this.specification = specification;
            this.givenTuple = givenTuple;
            this.factManager = factManager;
            this.logger = loggerFactory.CreateLogger<Observer>();

            // Add the initial handler.
            addedHandlers = addedHandlers.Add(new AddedHandler(givenTuple, "", onAdded));

            // Identify a specification by its hash.
            specificationHash = IdentityUtilities.ComputeSpecificationHash(specification, givenTuple);
        }

        public Task<bool> Cached => cachedTask ?? Task.FromResult(false);
        public Task Loaded => loadedTask ?? Task.CompletedTask;

        internal void Start()
        {
            logger.LogInformation("Observer starting for {Specification}", specification.ToDescriptiveString());

            // Capture the synchronization context so that notifications
            // can be executed on the same thread.
            synchronizationContext = SynchronizationContext.Current;

            var cancellationToken = cancelInitialize.Token;
            cachedTask = Task.Run(async () =>
                await ReadFromStore(cancellationToken).ConfigureAwait(false));
            loadedTask = cachedTask;
        }

        public virtual Task Refresh(CancellationToken? cancellationToken)
        {
            return cachedTask ?? Task.CompletedTask;
        }

        protected async Task<bool> ReadFromStore(CancellationToken cancellationToken)
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

        public void OnAdded(FactReferenceTuple anchor, string path, Func<object, Task<Func<Task>>> added)
        {
            lock (this)
            {
                addedHandlers = addedHandlers.Add(new AddedHandler(anchor, path, added));
            }
        }

        public FactReferenceTuple AnchorOf(string path, Product product)
        {
            return ResultSubsetAt(path).Of(product);
        }

        private Subset GivenSubset()
        {
            return specification.Givens
                .Select(g => g.Label.Name)
                .Aggregate(Subset.Empty, (subset, name) => subset.Add(name));
        }

        /// <summary>
        /// The subset of labels that identify a row at <paramref name="path"/>:
        /// the given labels, plus the unknowns of the matches at that path and at
        /// every path above it. This is the subset the inverter assigns to the same
        /// path, so an inverse's ResultSubset and this agree by construction.
        /// </summary>
        private Subset ResultSubsetAt(string path)
        {
            var subset = Inverter.AddUnknowns(GivenSubset(), specification.Matches);
            var projection = specification.Projection;
            if (string.IsNullOrEmpty(path))
            {
                return subset;
            }

            foreach (var name in path.Split('.'))
            {
                var collectionProjection = Inverter.CollectionsOf(projection, "")
                    .Where(pair => pair.Item1 == name)
                    .Select(pair => pair.Item2)
                    .FirstOrDefault();
                if (collectionProjection == null)
                {
                    throw new ArgumentException($"The specification has no collection at the path {path}.");
                }

                subset = Inverter.AddUnknowns(subset, collectionProjection.Matches);
                projection = collectionProjection.Projection;
            }
            return subset;
        }

        public void Stop()
        {
            logger.LogInformation("Observer stopping for {Specification}", specification.ToDescriptiveString());

            cancelInitialize.Cancel();
            foreach (var listener in listeners)
            {
                factManager.RemoveSpecificationListener(listener);
            }
            cancelInitialize.Dispose();
            factManager.Unsubscribe(feeds);
            feeds = ImmutableList<string>.Empty;
        }

        protected async Task Read(CancellationToken cancellationToken)
        {
            var results = await factManager.Read(givenTuple, specification, specification.Projection.Type, this, cancellationToken).ConfigureAwait(false);
            AddSpecificationListeners();
            var givenSubset = GivenSubset();
            var resultSubset = ResultSubsetAt("");

            await SynchronizeNotifyAdded(results, givenSubset, resultSubset, specification.Projection).ConfigureAwait(false);
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
                await SynchronizeNotifyAdded(results, inverse.ParentSubset, inverse.ResultSubset, projection).ConfigureAwait(false);
            }
            else if (inverse.Operation == InverseOperation.Remove || inverse.Operation == InverseOperation.MaybeRemove)
            {
                await SynchronizeNotifyRemoved(inverse, matchingProducts).ConfigureAwait(false);
            }
        }

        private async Task SynchronizeNotifyAdded(ImmutableList<ProjectedResult> results, Subset parentSubset, Subset resultSubset, Projection projection)
        {
            await SynchronizeOperaton(() => NotifyAdded(results, parentSubset, resultSubset, projection)).ConfigureAwait(false);
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

        private async Task NotifyAdded(ImmutableList<ProjectedResult> results, Subset parentSubset, Subset resultSubset, Projection projection)
        {
            foreach (var result in results)
            {
                var parentTuple = parentSubset.Of(result.Product);
                // A row is identified at this path by the labels of the path's result
                // subset. An inverse may bind more labels than that, such as the witness
                // of an existential condition, and those do not identify the row.
                var resultTuple = resultSubset.Of(result.Product);
                var matchingAddedHandlers = addedHandlers
                    .Where(hander => hander.Anchor.Equals(parentTuple) && hander.Path == result.Path);
                foreach (var addedHandler in matchingAddedHandlers)
                {
                    var resultAdded = addedHandler.Added;
                    // Don't call result added if the row already has a state: either
                    // its handler is running or it has already been delivered.
                    if (!ClaimRow(resultTuple))
                    {
                        continue;
                    }

                    Func<Task> removal;
                    try
                    {
                        removal = await resultAdded(result.Projection).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // The handler never produced a removal function, so the row
                        // was never delivered. Drop it, which leaves a later
                        // notification for the same row free to call the handler
                        // again.
                        lock (this)
                        {
                            rowStates = rowStates.Remove(resultTuple);
                        }

                        // The observer is the boundary between Jinaga and caller
                        // code, so a handler that throws is confined to its own row.
                        // Rethrowing would end this loop, end the notification of
                        // every later listener including other observers', and fail
                        // a save that has already committed, none of which the code
                        // that saved the fact did anything to deserve. Log it and
                        // carry on with the next row.
                        logger.LogError(ex, "Added handler threw for {Specification}", specification.ToDescriptiveString());
                        continue;
                    }

                    await SettleRow(resultTuple, removal).ConfigureAwait(false);
                }

                // Recursively notify added for specification results.
                if (result.Collections.Any())
                {
                    foreach (var (name, collectionProjection) in Inverter.CollectionsOf(projection, ""))
                    {
                        var collection = result.Collections
                            .FirstOrDefault(c => c.Name == name);
                        if (collection == null)
                        {
                            continue;
                        }

                        // The child path's result subset is this path's plus the labels
                        // that the collection's own matches bind, exactly as the inverter
                        // derives the subset of a nested path.
                        var collectionSubset = Inverter.AddUnknowns(resultSubset, collectionProjection.Matches);
                        await NotifyAdded(collection.Results, resultSubset, collectionSubset, collectionProjection.Projection).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Takes the row for this notification, marking it in flight for the
        /// duration of the caller's added handler. A row that already has a state
        /// belongs to another notification, so this returns false and the handler
        /// is not called.
        /// </summary>
        private bool ClaimRow(FactReferenceTuple resultTuple)
        {
            lock (this)
            {
                if (rowStates.ContainsKey(resultTuple))
                {
                    return false;
                }

                rowStates = rowStates.Add(resultTuple, new RowInFlight(removalRequested: false));
                return true;
            }
        }

        /// <summary>
        /// Settles a row whose added handler has just returned its removal
        /// function. Normally the row becomes delivered and holds that function
        /// until a removal arrives. A removal that arrived while the handler was
        /// running has no function to call at the time, so it is recorded on the
        /// in-flight row and honored here instead: the function is called once and
        /// the row does not enter the observed state.
        /// </summary>
        private async Task SettleRow(FactReferenceTuple resultTuple, Func<Task> removal)
        {
            bool removalRequested;
            lock (this)
            {
                removalRequested =
                    rowStates.TryGetValue(resultTuple, out var state) &&
                    state is RowInFlight inFlight &&
                    inFlight.RemovalRequested;
                if (removalRequested)
                {
                    RetireRow(resultTuple);
                }
                else
                {
                    rowStates = rowStates.SetItem(resultTuple, new RowDelivered(removal));
                }
            }
            if (removalRequested)
            {
                await removal().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Drops a row and the added handlers that belong to it. Call while holding
        /// the lock.
        ///
        /// A handler's lifetime is the lifetime of the row whose projection
        /// registered it. The caller has been given this row's removal function and
        /// has no reason to expect its collections to keep receiving facts, so drop
        /// the handlers the row registered, and those of every row nested beneath
        /// it, whose anchors extend this row's tuple.
        /// </summary>
        private void RetireRow(FactReferenceTuple resultTuple)
        {
            rowStates = rowStates.Remove(resultTuple);
            addedHandlers = addedHandlers
                .Where(handler => !AnchorIsWithinRow(handler.Anchor, resultTuple))
                .ToImmutableList();
        }

        /// <summary>
        /// Determines whether an added handler belongs to the given row, either
        /// because the row's own projection registered it or because a row nested
        /// within it did. A nested row's anchor extends its ancestor's tuple, so
        /// agreement on every label of the row's tuple identifies both.
        /// </summary>
        private static bool AnchorIsWithinRow(FactReferenceTuple anchor, FactReferenceTuple rowTuple)
        {
            foreach (var name in rowTuple.Names)
            {
                if (!anchor.Names.Contains(name) ||
                    !anchor.Get(name).Equals(rowTuple.Get(name)))
                {
                    return false;
                }
            }
            return true;
        }

        private async Task NotifyRemoved(ImmutableList<Product> products, Subset resultSubset)
        {
            foreach (var product in products)
            {
                var resultTuple = resultSubset.Of(product);
                // Retire the row before awaiting its removal function. That function
                // is the caller's, so it may yield, and anything the observer does
                // while it is in flight must already see the row as gone. Claiming it
                // under the lock is also what stops two concurrent removals of one row
                // from both invoking it.
                Func<Task>? removal = null;
                lock (this)
                {
                    if (rowStates.TryGetValue(resultTuple, out var state))
                    {
                        if (state is RowDelivered delivered)
                        {
                            // The row is delivered, so its removal function is known.
                            // Dropping the row here also means a later product that
                            // satisfies the specification again is not a duplicate.
                            removal = delivered.Removal;
                            RetireRow(resultTuple);
                        }
                        else
                        {
                            // The added handler has not returned a removal function
                            // yet. Record the request against the in-flight row so
                            // that SettleRow honors it. A second removal in the same
                            // interval finds the request already recorded, so the
                            // function is still called exactly once.
                            rowStates = rowStates.SetItem(
                                resultTuple, new RowInFlight(removalRequested: true));
                        }
                    }
                    // A row with no state was never notified, so there is nothing to
                    // remove and nothing to remember.
                }
                if (removal != null)
                {
                    await removal().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// The state of one row of the observed result set.
        ///
        /// A row the observer has notified is in exactly one of two states: its
        /// added handler is still running, or that handler has returned the
        /// function to call when the row is removed. A row the observer has not
        /// notified has no state, which is the absence of an entry rather than a
        /// third case.
        /// </summary>
        private abstract class RowState
        {
        }

        /// <summary>
        /// The row's added handler has been called and has not returned, so the
        /// observer does not yet hold a function to call when the row is removed.
        /// A removal that arrives in that interval is recorded here and honored as
        /// soon as the handler supplies one.
        /// </summary>
        private sealed class RowInFlight : RowState
        {
            internal RowInFlight(bool removalRequested)
            {
                RemovalRequested = removalRequested;
            }

            internal bool RemovalRequested { get; }
        }

        /// <summary>
        /// The row's added handler has returned, and the function it returned is
        /// the one to call when the row is removed.
        /// </summary>
        private sealed class RowDelivered : RowState
        {
            internal RowDelivered(Func<Task> removal)
            {
                Removal = removal;
            }

            internal Func<Task> Removal { get; }
        }
    }
}
