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
        private ImmutableList<FactListener> givenListeners =
            ImmutableList<FactListener>.Empty;
        private ImmutableList<AddedHandler> addedHandlers =
            ImmutableList<AddedHandler>.Empty;
        private ImmutableDictionary<FactReferenceTuple, RowState> rowStates =
            ImmutableDictionary<FactReferenceTuple, RowState>.Empty;
        protected ImmutableList<string> feeds = ImmutableList<string>.Empty;

        /// <summary>
        /// The number of the most recent read. Every read that decides rows -- the initial
        /// read, a candidate's re-check, and the read taken when a given is saved -- takes the
        /// next number before it touches the store, so a higher number is a read that began
        /// later.
        /// </summary>
        private long lastRead;

        /// <summary>
        /// The reads whose rows are still being delivered: from before each read touches the
        /// store until every row of its result, at every depth, has been delivered. A removal
        /// decided while an older read is pending is recorded as
        /// <see cref="RowRemovedBeforeDelivery"/>, so that the older read cannot deliver the
        /// row again.
        /// </summary>
        private ImmutableList<long> pendingReads = ImmutableList<long>.Empty;

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
                factManager.RemoveInverseListener(listener);
            }
            foreach (var givenListener in givenListeners)
            {
                factManager.RemoveFactListener(givenListener);
            }
            cancelInitialize.Dispose();
            factManager.Unsubscribe(feeds);
            feeds = ImmutableList<string>.Empty;
        }

        protected async Task Read(CancellationToken cancellationToken)
        {
            // Register the listeners before the read rather than after it. A fact
            // saved while the read is in flight is in neither the read's result set
            // nor a listener's reach when the listeners come second, so the watch
            // never shows it. Coming first, the listener delivers it. A fact that
            // lands in both the result set and a notification is still delivered
            // once, because ClaimRow refuses the second of the two.
            // The read is numbered before the listeners are registered, so every
            // decision a listener makes from here on comes from a later read.
            var read = BeginRead();
            try
            {
                AddSpecificationListeners();
                var results = await factManager.Read(givenTuple, specification, specification.Projection.Type, this, cancellationToken).ConfigureAwait(false);
                var givenSubset = GivenSubset();
                var resultSubset = ResultSubsetAt("");

                await SynchronizeNotifyAdded(results, givenSubset, resultSubset, specification.Projection, read).ConfigureAwait(false);
            }
            finally
            {
                EndRead(read);
            }
        }

        /// <summary>
        /// Numbers a read that is about to decide rows, and marks it pending until
        /// <see cref="EndRead"/>. Call before the read touches the store.
        /// </summary>
        private long BeginRead()
        {
            lock (this)
            {
                var read = ++lastRead;
                pendingReads = pendingReads.Add(read);
                return read;
            }
        }

        /// <summary>
        /// Marks a read's rows delivered, and drops the withdrawals that no pending read is
        /// old enough to need. A withdrawal cancels only a delivery from a read that began
        /// before the removal was decided, and once no such read is pending, it would cancel
        /// nothing but a later legitimate add.
        /// </summary>
        private void EndRead(long read)
        {
            lock (this)
            {
                pendingReads = pendingReads.Remove(read);
                // A withdrawal is kept while a read older than it is pending, so only the
                // end of the oldest pending read can release any.
                if (IsOlderReadPending(read))
                {
                    return;
                }
                var undelivered = rowStates
                    .Where(pair =>
                        pair.Value is RowRemovedBeforeDelivery withdrawn &&
                        !IsOlderReadPending(withdrawn.Read))
                    .Select(pair => pair.Key)
                    .ToImmutableList();
                rowStates = rowStates.RemoveRange(undelivered);
            }
        }

        /// <summary>
        /// Whether a read that began before this one is still delivering rows. Call while
        /// holding the lock.
        /// </summary>
        private bool IsOlderReadPending(long read)
        {
            return pendingReads.Any(pending => pending < read);
        }

        /// <summary>
        /// Registers a listener for every way a saved fact can change what this observer
        /// holds: the candidate inverses of the specification at each path of the
        /// projection, and one per given for the observer's own givens.
        /// </summary>
        /// <remarks>
        /// An inverse of <see cref="InverseCandidates"/> carries no operation. Every row it
        /// reports is a candidate, and <see cref="OnCandidates"/> decides each against the
        /// store rather than trusting an add or a remove.
        /// </remarks>
        private void AddSpecificationListeners()
        {
            var listeners = ImmutableList<SpecificationListener>.Empty;
            foreach (var flattened in FlattenedSpecifications())
            {
                foreach (var inverse in InverseCandidates.CandidateInverses(flattened.Specification))
                {
                    listeners = listeners.Add(factManager.AddInverseListener(
                        inverse,
                        givenTuple,
                        async (ImmutableList<Product> products, CancellationToken cancellationToken) =>
                            await OnCandidates(flattened, products, cancellationToken).ConfigureAwait(false)));
                }
            }
            // One listener per given, keyed by the given's own reference rather than by its
            // type. The question a saved fact of a given's type raises is whether it is
            // this given, and that is answered by reference before anything is read: a
            // listener keyed by the type would read the whole specification for every such
            // fact and discard all but the rows of the one that matched.
            var givenListeners = ImmutableList<FactListener>.Empty;
            foreach (var reference in specification.Givens
                .Select(given => givenTuple.Get(given.Label.Name))
                .Distinct())
            {
                givenListeners = givenListeners.Add(factManager.AddFactListener(
                    reference,
                    async (CancellationToken cancellationToken) =>
                        await OnGivenSaved(cancellationToken).ConfigureAwait(false)));
            }
            this.listeners = listeners;
            this.givenListeners = givenListeners;
        }

        /// <summary>
        /// The specification at each path of the projection: the top level, and each
        /// collection flattened into a specification of its own -- the matches of every
        /// level above it followed by its own, with its own projection. The candidate
        /// inverses of each report the rows of that path.
        /// </summary>
        private ImmutableList<FlatSpecification> FlattenedSpecifications()
        {
            var resultSubset = ResultSubsetAt("");
            return ImmutableList
                .Create(new FlatSpecification(
                    "", specification, specification.Projection, GivenSubset(), resultSubset))
                .AddRange(CollectionSpecifications(
                    "", specification.Matches, specification.Projection, resultSubset));
        }

        private ImmutableList<FlatSpecification> CollectionSpecifications(
            string path, ImmutableList<Match> matches, Projection projection, Subset resultSubset)
        {
            var flattened = ImmutableList<FlatSpecification>.Empty;
            foreach (var (name, collectionProjection) in Inverter.CollectionsOf(projection, ""))
            {
                var childPath = string.IsNullOrEmpty(path) ? name : $"{path}.{name}";
                var childMatches = matches.AddRange(collectionProjection.Matches);
                var childSubset = Inverter.AddUnknowns(resultSubset, collectionProjection.Matches);
                flattened = flattened
                    .Add(new FlatSpecification(
                        childPath,
                        new Specification(specification.Givens, childMatches, collectionProjection.Projection),
                        collectionProjection.Projection,
                        resultSubset,
                        childSubset))
                    .AddRange(CollectionSpecifications(
                        childPath, childMatches, collectionProjection.Projection, childSubset));
            }
            return flattened;
        }

        /// <summary>
        /// Decides each candidate the inverse reported, and notifies only a row whose
        /// membership changed.
        /// </summary>
        /// <remarks>
        /// Candidates of one saved batch may be decided in any order, so each is decided on
        /// its own. One inverse can report a row more than once, and several inverses can
        /// report the same row, so a row already decided in this batch is not read again.
        /// </remarks>
        private async Task OnCandidates(FlatSpecification flattened, ImmutableList<Product> products, CancellationToken cancellationToken)
        {
            var decided = ImmutableHashSet<FactReferenceTuple>.Empty;
            foreach (var product in products)
            {
                // A row is identified at this path by that path's result subset. An inverse
                // binds more labels than those -- the fact it is pinned on, and the labels it
                // lifted out of an existential condition -- and none of those identify a row.
                var candidate = flattened.ResultSubset.Of(product);
                if (decided.Contains(candidate))
                {
                    continue;
                }
                decided = decided.Add(candidate);

                await DecideCandidate(flattened, candidate, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Reads the specification for one candidate row and notifies the transition, if
        /// there is one. A row the read holds is added, unless it is held already; a row the
        /// read does not hold is removed, unless it was never delivered. Both of those
        /// exceptions are the row-state machine's, so a row that did not change is left
        /// exactly as it is.
        /// </summary>
        private async Task DecideCandidate(FlatSpecification flattened, FactReferenceTuple candidate, CancellationToken cancellationToken)
        {
            var read = BeginRead();
            try
            {
                var (pinned, pinnedGivens) = CandidateRecheck.Pin(
                    flattened.Specification, givenTuple, candidate);
                var rows = await factManager.QueryLocal(pinnedGivens, pinned, cancellationToken).ConfigureAwait(false);
                if (rows.IsEmpty)
                {
                    await SynchronizeNotifyRemovedRow(candidate, read).ConfigureAwait(false);
                }
                else
                {
                    var projection = flattened.Projection;
                    var results = await factManager.ComputeProjections(
                        projection, rows, projection.Type, this, flattened.Path, cancellationToken).ConfigureAwait(false);
                    await SynchronizeNotifyAdded(
                        results, flattened.ParentSubset, flattened.ResultSubset, projection, read).ConfigureAwait(false);
                }
            }
            finally
            {
                EndRead(read);
            }
        }

        /// <summary>
        /// Takes the rows of the whole specification, because one of this observer's givens
        /// was saved. Every row of that read and every row the observer holds is a candidate,
        /// and the one read decides them all, so this is the one case that legitimately reads
        /// the whole specification.
        /// </summary>
        /// <remarks>
        /// A row reachable only by walking a given -- its predecessors, or facts joined
        /// through one of them -- already exists when the given is saved, so no candidate
        /// inverse ever runs for it: a candidate inverse is pinned on an unknown, and the
        /// givens are not unknowns. This is what <c>Inverter.CreateSelfInverse</c> answered,
        /// and it could be registered only for a specification of exactly one given carrying
        /// no conditions, because the listener it used was keyed by that single given's type.
        /// One listener per given reference is subject to neither limit, and it reads only
        /// when the fact saved is a given of this observer's own.
        /// </remarks>
        private async Task OnGivenSaved(CancellationToken cancellationToken)
        {
            var read = BeginRead();
            try
            {
                var projection = specification.Projection;
                var results = await factManager.Read(
                    givenTuple, specification, projection.Type, this, cancellationToken).ConfigureAwait(false);
                var present = RowsOf(results);
                ImmutableList<FactReferenceTuple> absent;
                lock (this)
                {
                    absent = rowStates.Keys
                        .Where(row => !present.Contains(row))
                        .ToImmutableList();
                }
                foreach (var row in absent)
                {
                    await SynchronizeNotifyRemovedRow(row, read).ConfigureAwait(false);
                }
                await SynchronizeNotifyAdded(
                    results, GivenSubset(), ResultSubsetAt(""), projection, read).ConfigureAwait(false);
            }
            finally
            {
                EndRead(read);
            }
        }

        /// <summary>
        /// The tuple that identifies each row of a read, at every depth of its projection.
        /// </summary>
        private ImmutableHashSet<FactReferenceTuple> RowsOf(ImmutableList<ProjectedResult> results)
        {
            var rows = ImmutableHashSet<FactReferenceTuple>.Empty;
            void Collect(ImmutableList<ProjectedResult> list)
            {
                foreach (var result in list)
                {
                    rows = rows.Add(ResultSubsetAt(result.Path).Of(result.Product));
                    foreach (var collection in result.Collections)
                    {
                        Collect(collection.Results);
                    }
                }
            }
            Collect(results);
            return rows;
        }

        /// <param name="read">
        /// The read these results came from, at every depth of their projection. A removal
        /// decided by a later read withdraws their delivery; one decided by an earlier read
        /// does not.
        /// </param>
        private async Task SynchronizeNotifyAdded(ImmutableList<ProjectedResult> results, Subset parentSubset, Subset resultSubset, Projection projection, long read)
        {
            await SynchronizeOperaton(() => NotifyAdded(results, parentSubset, resultSubset, projection, read)).ConfigureAwait(false);
        }

        private async Task SynchronizeNotifyRemovedRow(FactReferenceTuple resultTuple, long read)
        {
            await SynchronizeOperaton(() => NotifyRemovedRow(resultTuple, read)).ConfigureAwait(false);
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

        private async Task NotifyAdded(ImmutableList<ProjectedResult> results, Subset parentSubset, Subset resultSubset, Projection projection, long read)
        {
            foreach (var result in results)
            {
                // A row is identified at this path by the labels of the path's result
                // subset. An inverse may bind more labels than that, such as the witness
                // of an existential condition, and those do not identify the row.
                var resultTuple = resultSubset.Of(result.Product);
                await NotifyAddedRow(result, resultTuple, parentSubset, resultSubset, projection, read).ConfigureAwait(false);
            }
        }

        private async Task NotifyAddedRow(ProjectedResult result, FactReferenceTuple resultTuple, Subset parentSubset, Subset resultSubset, Projection projection, long read)
        {
            var parentTuple = parentSubset.Of(result.Product);
            var matchingAddedHandlers = addedHandlers
                .Where(hander => hander.Anchor.Equals(parentTuple) && hander.Path == result.Path);
            foreach (var addedHandler in matchingAddedHandlers)
            {
                var resultAdded = addedHandler.Added;
                // Don't call result added if the row already has a state: either
                // its handler is running or it has already been delivered.
                if (!ClaimRow(resultTuple, read))
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

                await SettleRow(resultTuple, removal, read).ConfigureAwait(false);
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
                    // The rows of the collections come out of the same read as this row, so
                    // a removal decided for one of them since that read began withdraws
                    // its delivery, however long this row's handler took.
                    await NotifyAdded(collection.Results, resultSubset, collectionSubset, collectionProjection.Projection, read).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Takes the row for this notification, marking it in flight for the
        /// duration of the caller's added handler. A row that already has a state
        /// belongs to another notification, so this returns false and the handler
        /// is not called.
        ///
        /// Whichever read decided a row last is the one that counts. A removal
        /// decided by a later read than this one cancels this delivery: the row was
        /// gone by the time this read's result is being delivered. A removal decided
        /// by an earlier read does not, because this read found the row present
        /// since, and dropping that add is what loses a row the store holds -- the
        /// watch never shows it while a query returns it. For the same reason, this
        /// read overrides an earlier read's removal still waiting on a running
        /// handler, and a held row remembers this read so that an earlier read's
        /// removal arriving late leaves it alone.
        /// </summary>
        private bool ClaimRow(FactReferenceTuple resultTuple, long read)
        {
            lock (this)
            {
                if (!rowStates.TryGetValue(resultTuple, out var state))
                {
                    rowStates = rowStates.Add(resultTuple, new RowInFlight(read, removalRead: null));
                    return true;
                }

                switch (state)
                {
                    case RowRemovedBeforeDelivery withdrawn when withdrawn.Read < read:
                        rowStates = rowStates.SetItem(resultTuple, new RowInFlight(read, removalRead: null));
                        return true;
                    case RowInFlight inFlight when inFlight.RemovalRead < read:
                        rowStates = rowStates.SetItem(resultTuple, new RowInFlight(read, removalRead: null));
                        return false;
                    case RowInFlight inFlight when inFlight.Read < read:
                        rowStates = rowStates.SetItem(resultTuple, new RowInFlight(read, inFlight.RemovalRead));
                        return false;
                    case RowDelivered delivered when delivered.Read < read:
                        rowStates = rowStates.SetItem(resultTuple, new RowDelivered(read, delivered.Removal));
                        return false;
                    default:
                        return false;
                }
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
        private async Task SettleRow(FactReferenceTuple resultTuple, Func<Task> removal, long read)
        {
            bool removalRequested = false;
            lock (this)
            {
                if (rowStates.TryGetValue(resultTuple, out var state) &&
                    state is RowInFlight inFlight)
                {
                    if (inFlight.RemovalRead is long removalRead)
                    {
                        removalRequested = true;
                        RetireRow(resultTuple, removalRead);
                    }
                    else
                    {
                        rowStates = rowStates.SetItem(resultTuple, new RowDelivered(inFlight.Read, removal));
                    }
                }
                else
                {
                    rowStates = rowStates.SetItem(resultTuple, new RowDelivered(read, removal));
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
        ///
        /// A read older than the removal may still be delivering this row, out of a
        /// result computed while it was present. The row keeps a withdrawal in that
        /// case, so that delivery does not bring it back.
        /// </summary>
        private void RetireRow(FactReferenceTuple resultTuple, long read)
        {
            rowStates = IsOlderReadPending(read)
                ? rowStates.SetItem(resultTuple, new RowRemovedBeforeDelivery(read))
                : rowStates.Remove(resultTuple);
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

        private async Task NotifyRemovedRow(FactReferenceTuple resultTuple, long read)
        {
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
                    if (state is RowDelivered delivered && delivered.Read <= read)
                    {
                        // The row is delivered, so its removal function is known.
                        // Dropping the row here also means a later product that
                        // satisfies the specification again is not a duplicate.
                        removal = delivered.Removal;
                        RetireRow(resultTuple, read);
                    }
                    else if (state is RowInFlight inFlight && inFlight.Read <= read)
                    {
                        // The added handler has not returned a removal function
                        // yet. Record the request against the in-flight row so
                        // that SettleRow honors it. A second removal in the same
                        // interval finds the request already recorded, so the
                        // function is still called exactly once.
                        rowStates = rowStates.SetItem(
                            resultTuple, new RowInFlight(inFlight.Read, removalRead: read));
                    }
                    else if (state is RowRemovedBeforeDelivery withdrawn && withdrawn.Read < read)
                    {
                        // A row already withdrawn stays withdrawn, and the later
                        // removal withdraws it from every read older than itself.
                        rowStates = rowStates.SetItem(
                            resultTuple, new RowRemovedBeforeDelivery(read));
                    }
                }
                else if (IsOlderReadPending(read))
                {
                    // The row may yet be delivered by a read that began before
                    // this removal: the initial read, or the read that decided a
                    // row this one is nested within, whose collections have not
                    // been reached. Remember the removal against the row, and
                    // ClaimRow cancels that delivery when it arrives.
                    rowStates = rowStates.Add(
                        resultTuple, new RowRemovedBeforeDelivery(read));
                }
                // A held row that a later read found present is left alone: this
                // removal is older than that decision.
                //
                // With no older read pending, a row with no state was never
                // notified and nothing is about to notify it, so there is
                // nothing to remove and nothing to remember. Recording a
                // removal here would cancel the row's next legitimate add.
            }
            if (removal != null)
            {
                await removal().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// The specification at one path of the projection, flattened: the matches of every
        /// level above this path followed by this path's own, with this path's projection.
        /// </summary>
        private class FlatSpecification
        {
            internal FlatSpecification(string path, Specification specification, Projection projection, Subset parentSubset, Subset resultSubset)
            {
                Path = path;
                Specification = specification;
                Projection = projection;
                ParentSubset = parentSubset;
                ResultSubset = resultSubset;
            }

            /// <summary>The path of this projection, empty at the top level.</summary>
            internal string Path { get; }

            internal Specification Specification { get; }
            internal Projection Projection { get; }

            /// <summary>The labels that identify the row a handler at this path is anchored to.</summary>
            internal Subset ParentSubset { get; }

            /// <summary>The labels that identify a row at this path.</summary>
            internal Subset ResultSubset { get; }
        }

        /// <summary>
        /// The state of one row of the observed result set.
        ///
        /// A row the observer has notified is in one of two states: its added
        /// handler is still running, or that handler has returned the function to
        /// call when the row is removed. A row the observer has not notified has
        /// no state, which is the absence of an entry rather than a further case.
        ///
        /// The third state belongs to a row that is not held but that a pending read
        /// may still deliver: a removal reached it while a read older than the
        /// removal was in flight. That state means something only while such a read
        /// is pending, so it is recorded only then and dropped when the last of them
        /// ends.
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
            internal RowInFlight(long read, long? removalRead)
            {
                Read = read;
                RemovalRead = removalRead;
            }

            /// <summary>The latest read that decided the row present.</summary>
            internal long Read { get; }

            /// <summary>
            /// The read that decided a removal while the handler was running, or null
            /// when no removal is waiting for it.
            /// </summary>
            internal long? RemovalRead { get; }
        }

        /// <summary>
        /// The row's added handler has returned, and the function it returned is
        /// the one to call when the row is removed.
        /// </summary>
        private sealed class RowDelivered : RowState
        {
            internal RowDelivered(long read, Func<Task> removal)
            {
                Read = read;
                Removal = removal;
            }

            /// <summary>The latest read that decided the row present.</summary>
            internal long Read { get; }

            internal Func<Task> Removal { get; }
        }

        /// <summary>
        /// The row was removed while a read that began before the removal was still
        /// delivering rows: either it was never delivered, or it was delivered and
        /// then retired. That older read may still deliver the row out of a result
        /// computed while it was present, and ClaimRow cancels such a delivery
        /// rather than calling the handler for a row that is already gone.
        /// </summary>
        private sealed class RowRemovedBeforeDelivery : RowState
        {
            internal RowRemovedBeforeDelivery(long read)
            {
                Read = read;
            }

            /// <summary>The read that decided the removal.</summary>
            internal long Read { get; }
        }
    }
}
