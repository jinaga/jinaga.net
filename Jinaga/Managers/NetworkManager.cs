using Jinaga.Authorization;
using Jinaga.Facts;
using Jinaga.Identity;
using Jinaga.Projections;
using Jinaga.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Managers
{
    class NetworkManager
    {
        private readonly INetwork network;
        private readonly IStore store;
        private readonly ILogger logger;
        private readonly Func<FactGraph, CancellationToken, Task> saveAndNotify;
        private readonly int maxBatchSize;
        private readonly IIdentitySource? identitySource;
        private readonly AuthorizationEngine? authorizationEngine;

        private ImmutableDictionary<string, Task<(ImmutableList<string> feeds, ImmutableList<FeedDecision> decisions)>> feedsCache =
            ImmutableDictionary<string, Task<(ImmutableList<string> feeds, ImmutableList<FeedDecision> decisions)>>.Empty;
        private ImmutableDictionary<string, Task> activeFeeds =
            ImmutableDictionary<string, Task>.Empty;
        private ImmutableDictionary<string, Subscriber> subscribers =
            ImmutableDictionary<string, Subscriber>.Empty;
        private int fetchCount = 0;
        private LoadBatch? currentBatch = null;
        private JinagaStatus status = JinagaStatus.Default;

        public event JinagaStatusChanged? OnStatusChanged;
        public event DistributionDiagnosticHandler? OnDistributionDiagnostic;

        public NetworkManager(INetwork network, IStore store, ILoggerFactory loggerFactory, Func<FactGraph, CancellationToken, Task> saveAndNotify, int maxBatchSize)
            : this(network, store, loggerFactory, saveAndNotify, maxBatchSize, null, null)
        {
        }

        /// <param name="identitySource">Reports who is logged in, or null if the client cannot tell</param>
        /// <param name="authorizationEngine">
        /// Evaluates the application's authorization rules, or null if the client was not given
        /// them. Used only to decide which queued facts another user may upload; it refuses
        /// nothing.
        /// </param>
        internal NetworkManager(INetwork network, IStore store, ILoggerFactory loggerFactory, Func<FactGraph, CancellationToken, Task> saveAndNotify, int maxBatchSize, IIdentitySource? identitySource, AuthorizationEngine? authorizationEngine)
        {
            if (maxBatchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBatchSize), maxBatchSize, "The maximum batch size must be greater than zero.");

            this.network = network;
            this.store = store;
            this.logger = loggerFactory.CreateLogger<NetworkManager>();
            this.saveAndNotify = saveAndNotify;
            this.maxBatchSize = maxBatchSize;
            this.identitySource = identitySource;
            this.authorizationEngine = authorizationEngine;

            network.OnAuthenticationStateChanged += SetAuthenticationState;
        }

        public async Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken)
        {
            return await network.Login(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Uploads the queued facts that may travel under the user who is logged in now.
        ///
        /// Facts held for another user stay in the queue, and are not counted in the queue
        /// length that <see cref="JinagaStatus.QueueLength"/> reports: when only held facts
        /// remain, the status reports nothing pending.
        /// </summary>
        public async Task Save(CancellationToken cancellationToken)
        {
            // The user the facts are sent under. Every attempt to send checks
            // that this user is still the one logged in, so a fact cannot leave
            // under the token of a user who logged in while it was being sent.
            var uploader = identitySource?.CurrentUser?.publicKey;
            var queue = await store.GetQueue().ConfigureAwait(false);
            queue = await SelectSendable(queue, uploader, cancellationToken).ConfigureAwait(false);
            if (queue.Facts.Count == 0)
            {
                SetSaveStatus(false, null, 0);
                return;
            }
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            logger.LogInformation("Save started with {0} facts.", queue.Facts.Count);
            SetSaveStatus(true, null, queue.Facts.Count);

            int remaining = queue.Facts.Count;
            try
            {
                // Send the facts in batches, so that a queue which has grown
                // past a request size limit between here and the replicator
                // still drains. A batch that succeeds is acknowledged, so a
                // failure part way through leaves the batches that succeeded
                // out of the queue and the rest in it.
                foreach (var batch in SplitIntoBatches(queue))
                {
                    if (batch.Graph.FactReferences.Count > 0)
                    {
                        // Checked here for an INetwork that does not check each
                        // attempt itself; HttpNetwork checks again before every one.
                        UploaderChangedException.ThrowIfChanged(identitySource, uploader);
                        await network.Save(batch.Graph, uploader, cancellationToken).ConfigureAwait(false);
                    }
                    await store.AcknowledgeQueue(batch.Facts).ConfigureAwait(false);
                    remaining -= batch.Count;
                    if (remaining > 0)
                    {
                        SetSaveStatus(true, null, remaining);
                    }
                }
                logger.LogInformation("Save completed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                SetSaveStatus(false, null, 0);
            }
            catch (UploaderChangedException)
            {
                // Not a failure. The facts that were not sent stay queued, and
                // the change of user has already scheduled the queue to be
                // processed again under whoever is logged in now.
                logger.LogInformation("Save stopped after {elapsedMilliseconds} ms because the user changed.", stopwatch.ElapsedMilliseconds);
                SetSaveStatus(false, null, remaining);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Save failed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                SetSaveStatus(false, ex, remaining);
                throw;
            }
        }

        /// <summary>
        /// The queued facts that may be uploaded under <paramref name="uploader"/>.
        ///
        /// A queued fact is held when somebody saved it, the uploader is not among them, and the
        /// rules would not admit it without a signature. Without rules, nothing is known to be
        /// admitted without a signature, so every fact another user saved is held. With rules,
        /// a fact whose rule is <c>Any</c> may travel under anyone: the replicator keeps no
        /// signature for it, so uploading it neither attributes it to the uploader nor fails.
        /// A fact nobody saved goes under whoever is logged in.
        ///
        /// A fact is sent only if it and every queued ancestor in its graph may be, because the
        /// ancestors travel in the same request.
        /// </summary>
        private async Task<QueuedFacts> SelectSendable(QueuedFacts queue, string? uploader, CancellationToken cancellationToken)
        {
            // An indivisible queue comes from a store that records no savers,
            // and holding any of it back would split it.
            if (queue.Facts.IsEmpty || queue.IsIndivisible)
            {
                return queue;
            }

            var queuedReferences = queue.Facts
                .Select(fact => fact.Reference)
                .ToImmutableList();
            var savers = await store.LoadSavers(queuedReferences, cancellationToken).ConfigureAwait(false);
            if (savers.IsEmpty)
            {
                return queue;
            }

            var held = new HashSet<FactReference>();
            foreach (var reference in queuedReferences)
            {
                if (savers.TryGetValue(reference, out var saversOfFact) &&
                    !saversOfFact.IsEmpty &&
                    (uploader == null || !saversOfFact.Contains(uploader)) &&
                    !await AdmitsWithoutSignature(queue.Graph, reference, cancellationToken).ConfigureAwait(false))
                {
                    held.Add(reference);
                }
            }
            if (held.Count == 0)
            {
                return queue;
            }

            var available = new HashSet<FactReference>(queue.Graph.FactReferences);
            var sendable = queue.Facts
                .Where(fact => !held.Contains(fact.Reference) &&
                    !HasAncestorIn(queue.Graph, available, fact.Reference, held))
                .ToImmutableList();
            return new QueuedFacts(queue.Graph, sendable);
        }

        private async Task<bool> AdmitsWithoutSignature(FactGraph graph, FactReference reference, CancellationToken cancellationToken)
        {
            if (authorizationEngine == null)
            {
                return false;
            }
            return await authorizationEngine
                .AdmitsWithoutSignature(graph, reference, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Whether any strict ancestor of the fact, as the graph records it, is in the set.
        /// </summary>
        private static bool HasAncestorIn(FactGraph graph, HashSet<FactReference> available, FactReference reference, HashSet<FactReference> set)
        {
            var visited = new HashSet<FactReference>();
            var pending = new Stack<FactReference>();
            pending.Push(reference);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!available.Contains(current))
                {
                    continue;
                }
                foreach (var predecessor in graph.GetFact(current).GetAllPredecessorReferences())
                {
                    if (set.Contains(predecessor))
                    {
                        return true;
                    }
                    if (visited.Add(predecessor))
                    {
                        pending.Push(predecessor);
                    }
                }
            }
            return false;
        }

        private class SaveBatch
        {
            public FactGraph Graph { get; }

            /// <summary>
            /// The queue entries this batch covers, which are the ones to
            /// acknowledge once it has been sent.
            /// </summary>
            public ImmutableList<QueuedFact> Facts { get; }

            public int Count => Facts.Count;

            public SaveBatch(FactGraph graph, ImmutableList<QueuedFact> facts)
            {
                Graph = graph;
                Facts = facts;
            }
        }

        /// <summary>
        /// Divides the queue into consecutive batches of at most maxBatchSize
        /// facts, oldest first.
        ///
        /// A batch carries the ancestors of the facts it sends, including ones
        /// an earlier batch already sent, because the graph wire format names a
        /// predecessor by its position within the same request. The bound
        /// therefore applies to the whole request rather than to the facts that
        /// are new to it, which is what makes it a bound on request size.
        ///
        /// A single queued fact whose ancestors already exceed the bound is
        /// sent on its own and over the bound, so that the queue still drains.
        /// </summary>
        private IEnumerable<SaveBatch> SplitIntoBatches(QueuedFacts queue)
        {
            // A queue from a store built against an earlier Jinaga has one
            // bookmark for the whole graph, so it goes in one request, as it
            // did before batching.
            if (queue.IsIndivisible)
            {
                yield return new SaveBatch(queue.Graph, queue.Facts);
                yield break;
            }

            // A store can report a queued fact that its graph does not carry,
            // if an ancestor of that fact is missing. Such a fact could not be
            // sent before this change either, so the bookmark still moves past
            // it rather than stalling the queue on it.
            var available = new HashSet<FactReference>(queue.Graph.FactReferences);

            int index = 0;
            while (index < queue.Facts.Count)
            {
                var graph = FactGraph.Empty;
                var included = new HashSet<FactReference>();
                var facts = ImmutableList.CreateBuilder<QueuedFact>();
                while (index < queue.Facts.Count)
                {
                    var reference = queue.Facts[index].Reference;
                    if (available.Contains(reference))
                    {
                        var additions = AncestorsNotIncluded(queue.Graph, reference, included);
                        if (facts.Count > 0 && included.Count + additions.Count > maxBatchSize)
                        {
                            break;
                        }
                        foreach (var addition in additions)
                        {
                            graph = graph.Add(queue.Graph.GetEnvelope(addition));
                            included.Add(addition);
                        }
                    }
                    facts.Add(queue.Facts[index]);
                    index++;
                }
                if (included.Count > maxBatchSize)
                {
                    logger.LogWarning("A queued fact and its ancestors are {0} facts, which exceeds the maximum batch size of {1}. Sending it as one batch.", included.Count, maxBatchSize);
                }
                yield return new SaveBatch(graph, facts.ToImmutable());
            }
        }

        /// <summary>
        /// The fact and the ancestors it needs that the batch does not already
        /// have, in topological order.
        /// </summary>
        private static ImmutableList<FactReference> AncestorsNotIncluded(FactGraph graph, FactReference reference, HashSet<FactReference> included)
        {
            var additions = ImmutableList.CreateBuilder<FactReference>();
            var visited = new HashSet<FactReference>();
            Visit(graph, reference, included, visited, additions);
            return additions.ToImmutable();
        }

        private static void Visit(FactGraph graph, FactReference reference, HashSet<FactReference> included, HashSet<FactReference> visited, ImmutableList<FactReference>.Builder additions)
        {
            if (included.Contains(reference) || !visited.Add(reference))
            {
                return;
            }
            foreach (var predecessor in graph.GetFact(reference).GetAllPredecessorReferences())
            {
                Visit(graph, predecessor, included, visited, additions);
            }
            additions.Add(reference);
        }

        public async Task Fetch(FactReferenceTuple givenTuple, Specification specification, DistributionOperation operation, Action<DistributionDiagnostic>? collectDiagnostic, bool throwOnStructuralDenial, CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            SetLoadStatus(true, null);

            // The replicator's decisions are reported once per call, however many times the
            // feed list is resolved below.
            var report = ReportOnce(operation, collectDiagnostic);

            // Retry once. A feed might be cached and need to be removed and re-added.
            int retryCount = 2;

            while (true)
            {
                try
                {
                    await FetchInternal(givenTuple, specification, operation, report, throwOnStructuralDenial, stopwatch, cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (DistributionDeniedException ex)
                {
                    // A structural denial is the replicator's settled answer, not a transient
                    // failure, so retrying it would only ask the same question again.
                    logger.LogError(ex, "Fetch was denied after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                    SetLoadStatus(false, ex);
                    throw;
                }
                catch (Exception ex)
                {
                    retryCount--;
                    if (retryCount > 0)
                    {
                        logger.LogWarning(ex, "Fetch failed after {elapsedMilliseconds} ms. Retrying.", stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        logger.LogError(ex, "Fetch failed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                        SetLoadStatus(false, ex);
                        throw;
                    }
                }
            }
        }

        private async Task FetchInternal(FactReferenceTuple givenTuple, Specification specification, DistributionOperation operation, Action<Specification, ImmutableList<FeedDecision>> report, bool throwOnStructuralDenial, Stopwatch stopwatch, CancellationToken cancellationToken)
        {
            var reducedSpecification = specification.Reduce();
            var (feeds, decisions) = await GetFeedsFromCache(givenTuple, reducedSpecification, cancellationToken).ConfigureAwait(false);
            report(reducedSpecification, decisions);
            if (throwOnStructuralDenial)
            {
                ThrowIfStructurallyDenied(operation, reducedSpecification, decisions);
            }
            logger.LogInformation("Fetch from {0} feeds.", feeds.Count);

            // Fork to fetch from each feed.
            var tasks = feeds.Select(feed =>
            {
                lock (this)
                {
                    if (activeFeeds.TryGetValue(feed, out var task))
                    {
                        return task;
                    }
                    else
                    {
                        task = Task.Run(() => ProcessFeed(feed, cancellationToken));
                        activeFeeds = activeFeeds.Add(feed, task);
                        return task;
                    }
                }
            });

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
                logger.LogInformation("Fetch completed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                SetLoadStatus(false, null);
            }
            catch
            {
                // If any feed fails, then remove the specification from the cache.
                RemoveFeedsFromCache(givenTuple, reducedSpecification);
                throw;
            }
        }

        public async Task<ImmutableList<string>> Subscribe(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // The replicator's decisions are reported once per call, however many times the
            // feed list is resolved below.
            var report = ReportOnce(DistributionOperation.Subscribe, null);

            // Retry once. A feed might be cached and need to be removed and re-added.
            int retryCount = 2;

            while (true)
            {
                try
                {
                    return await SubscribeInternal(givenTuple, specification, report, stopwatch, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    retryCount--;
                    if (retryCount > 0)
                    {
                        logger.LogWarning(ex, "Subscribe failed after {elapsedMilliseconds} ms. Retrying.", stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        logger.LogError(ex, "Subscribe failed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                        throw;
                    }
                }
            }
        }

        private async Task<ImmutableList<string>> SubscribeInternal(FactReferenceTuple givenTuple, Specification specification, Action<Specification, ImmutableList<FeedDecision>> report, Stopwatch stopwatch, CancellationToken cancellationToken)
        {
            var reducedSpecification = specification.Reduce();
            var (feeds, decisions) = await GetFeedsFromCache(givenTuple, reducedSpecification, cancellationToken).ConfigureAwait(false);
            report(reducedSpecification, decisions);
            logger.LogInformation("Subscribe to {0} feeds.", feeds.Count);

            List<Subscriber> subscribers;
            lock (this)
            {
                subscribers = feeds.Select(feed =>
                {
                    if (!this.subscribers.TryGetValue(feed, out var subscriber))
                    {
                        subscriber = new Subscriber(feed, this.network, this.store, this.logger, this.saveAndNotify,
                            cancellationToken => RegisterFeedsAgain(givenTuple, reducedSpecification, cancellationToken));
                        this.subscribers = this.subscribers.Add(feed, subscriber);
                    }
                    return subscriber;
                }).ToList();
            }

            var tasks = subscribers.Select(async subscriber =>
            {
                if (subscriber.AddRef())
                {
                    await subscriber.Start();
                }
            });

            try
            {
                await Task.WhenAll(tasks);
                logger.LogInformation("Subscribe initialized after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Subscribe failed after {elapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
                // If any feed fails, then remove the specification from the cache.
                this.RemoveFeedsFromCache(givenTuple, reducedSpecification);
                this.Unsubscribe(feeds);
                throw e;
            }
            return feeds;
        }

        public void Unsubscribe(ImmutableList<string> feeds)
        {
            if (feeds.Count == 0)
            {
                return;
            }
            logger.LogInformation("Unsubscribe from {0} feeds.", feeds.Count);
            lock (this)
            {
                foreach (var feed in feeds)
                {
                    if (subscribers.TryGetValue(feed, out var subscriber))
                    {
                        if (subscriber.Release())
                        {
                            subscriber.Stop();
                            subscribers = subscribers.Remove(feed);
                        }
                    }
                }
            }
        }

        private async Task ProcessFeed(string feed, CancellationToken cancellationToken)
        {
            try
            {
                // Load the bookmark.
                string bookmark = await store.LoadBookmark(feed).ConfigureAwait(false);

                while (true)
                {
                    Interlocked.Increment(ref fetchCount);
                    bool decremented = false;

                    try
                    {
                        // Fetch facts from the feed starting at the bookmark.
                        (var factReferences, var nextBookmark) = await network.FetchFeed(feed, bookmark, cancellationToken).ConfigureAwait(false);

                        // If there are no facts, save the (possibly advanced) bookmark
                        // and end, so a subsequent fetch does not redundantly re-request
                        // from a stale bookmark.
                        if (factReferences.Count == 0)
                        {
                            bookmark = nextBookmark;
                            await store.SaveBookmark(feed, bookmark).ConfigureAwait(false);
                            break;
                        }

                        // Load the facts that I don't already have.
                        var knownFactReferences = await store.ListKnown(factReferences).ConfigureAwait(false);
                        var unknownFactReferences = factReferences.RemoveRange(knownFactReferences);
                        if (unknownFactReferences.Any())
                        {
                            var batch = GetCurrentBatch();
                            batch.Add(unknownFactReferences);
                            var finalFetchCount = Interlocked.Decrement(ref fetchCount);
                            decremented = true;
                            if (finalFetchCount == 0)
                            {
                                // This is the last fetch, so trigger the batch.
                                batch.Trigger();
                            }
                            await batch.Completed.ConfigureAwait(false);
                        }

                        // Update the bookmark.
                        bookmark = nextBookmark;
                        await store.SaveBookmark(feed, bookmark).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (!decremented)
                        {
                            var finalFetchCount = Interlocked.Decrement(ref fetchCount);
                            if (finalFetchCount == 0)
                            {
                                lock (this)
                                {
                                    if (currentBatch != null)
                                    {
                                        // This is the last fetch, so trigger the batch.
                                        currentBatch.Trigger();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                // Remove the feed from the active set on every exit path (success,
                // exception, or cancellation) so a transient failure does not
                // permanently disable the feed. A subsequent fetch will retry it.
                lock (this)
                {
                    activeFeeds = activeFeeds.Remove(feed);
                }
            }
        }

        private LoadBatch GetCurrentBatch()
        {
            lock (this)
            {
                var batch = currentBatch;
                if (batch == null)
                {
                    // Begin a new batch.
                    batch = new LoadBatch(network, saveAndNotify, BatchStarted);
                    currentBatch = batch;
                }

                return batch;
            }
        }

        private void BatchStarted(LoadBatch batch)
        {
            lock (this)
            {
                if (batch == currentBatch)
                {
                    currentBatch = null;
                }
            }
        }

        /// <summary>
        /// Reports each of the replicator's decisions to the diagnostic event and to the
        /// caller's own collector, at most once however many times it is called. A handler
        /// that throws is isolated: the caller asked for facts, not for the handler.
        /// </summary>
        /// <remarks>
        /// The specification is the one declared to the replicator, reduced, so the diagnostic
        /// quotes what the replicator decided about rather than what the caller wrote.
        /// </remarks>
        private Action<Specification, ImmutableList<FeedDecision>> ReportOnce(DistributionOperation operation, Action<DistributionDiagnostic>? collectDiagnostic)
        {
            bool reported = false;
            return (specification, decisions) =>
            {
                if (reported)
                {
                    return;
                }
                reported = true;
                foreach (var decision in decisions)
                {
                    var diagnostic = new DistributionDiagnostic(operation, specification.ToDescriptiveString(), decision);
                    collectDiagnostic?.Invoke(diagnostic);
                    try
                    {
                        OnDistributionDiagnostic?.Invoke(diagnostic);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "A distribution diagnostic handler threw.");
                    }
                }
            };
        }

        /// <summary>
        /// Throws if the replicator refused any feed for a structural reason, which no fact
        /// anyone later writes will change. The code decides, not the decision: a replicator
        /// can call a structural refusal reactive, and the code is the claim that can be checked.
        /// </summary>
        private static void ThrowIfStructurallyDenied(DistributionOperation operation, Specification specification, ImmutableList<FeedDecision> decisions)
        {
            var denials = decisions
                .Where(decision => decision.IsStructural)
                .Select(decision => new DistributionDiagnostic(operation, specification.ToDescriptiveString(), decision))
                .ToImmutableList();
            if (!denials.IsEmpty)
            {
                throw new DistributionDeniedException(denials);
            }
        }

        private Task<(ImmutableList<string> feeds, ImmutableList<FeedDecision> decisions)> GetFeedsFromCache(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            lock (this)
            {
                var hash = IdentityUtilities.ComputeSpecificationHash(specification, givenTuple);
                if (feedsCache.TryGetValue(hash, out var cached))
                {
                    // A cancelled declaration is not a faulted one, and a declaration made for a
                    // subscriber carries that subscriber's connection token, which its refresh
                    // timer cancels every few minutes. Serving either back would hand every later
                    // caller the same failure.
                    if (!cached.IsFaulted && !cached.IsCanceled && !IsUnsettled(cached))
                    {
                        return cached;
                    }
                    feedsCache = feedsCache.Remove(hash);
                }
                var feeds = network.Feeds(givenTuple, specification, cancellationToken);
                feedsCache = feedsCache.Add(hash, feeds);
                return feeds;
            }
        }

        /// <summary>
        /// Whether a completed declaration carries a decision other than authorized. The
        /// replicator decides per request, and a login, a later fact or a redeployed policy
        /// can change any of those answers, so such a declaration is made again rather than
        /// served from the cache.
        /// </summary>
        private static bool IsUnsettled(Task<(ImmutableList<string> feeds, ImmutableList<FeedDecision> decisions)> declaration)
        {
            return declaration.Status == TaskStatus.RanToCompletion &&
                declaration.Result.decisions.Any(decision => decision.Decision != DistributionDecision.Authorized);
        }

        /// <summary>
        /// Declares the feeds of a specification to the replicator again, after it has
        /// forgotten one of them. The cached feed list is evicted first, so a concurrent
        /// Fetch or Subscribe of the same specification waits for this declaration rather
        /// than reusing a feed the replicator no longer knows.
        /// </summary>
        private async Task RegisterFeedsAgain(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            RemoveFeedsFromCache(givenTuple, specification);
            await GetFeedsFromCache(givenTuple, specification, cancellationToken).ConfigureAwait(false);
        }

        private void RemoveFeedsFromCache(FactReferenceTuple givenTuple, Specification specification)
        {
            var hash = IdentityUtilities.ComputeSpecificationHash(specification, givenTuple);
            lock (this)
            {
                feedsCache = feedsCache.Remove(hash);
            }
        }

        private void SetLoadStatus(bool isLoading, Exception? lastLoadError)
        {
            lock (this)
            {
                status = status.WithLoadStatus(isLoading, lastLoadError);
                OnStatusChanged?.Invoke(status);
            }
        }

        private void SetSaveStatus(bool isSaving, Exception? lastSaveError, int queueLength)
        {
            lock (this)
            {
                status = status.WithSaveStatus(isSaving, lastSaveError, queueLength);
                OnStatusChanged?.Invoke(status);
            }
        }

        private void SetAuthenticationState(JinagaAuthenticationState authenticationState)
        {
            lock (this)
            {
                status = status.WithAuthenticationState(authenticationState);
                OnStatusChanged?.Invoke(status);
            }
        }
    }
}