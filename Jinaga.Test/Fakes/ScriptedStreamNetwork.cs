using Jinaga.Facts;
using Jinaga.Http;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Test.Fakes
{
    /// <summary>
    /// A fake network whose feed stream is driven by the test. A connection delivers the
    /// scripted response and then stays open, so the test can raise a mid-stream error on
    /// it, or fails immediately when the test has asked every connection to fail.
    /// </summary>
    internal class ScriptedStreamNetwork : INetwork
    {
        private readonly object gate = new object();
        private readonly List<(int threshold, TaskCompletionSource<bool> waiter)> connectWaiters = new();
        private readonly List<(int threshold, TaskCompletionSource<bool> waiter)> feedsWaiters = new();
        private readonly List<string> requestedBookmarks = new();
        private Action<Exception> currentErrorCallback;
        private int connectCount = 0;
        private int feedsCallCount = 0;
        private bool registered = true;

#pragma warning disable CS0067
        public event INetwork.AuthenticationStateChanged OnAuthenticationStateChanged;
#pragma warning restore CS0067

        /// <summary>
        /// The feed that <see cref="Feeds"/> registers.
        /// </summary>
        public string Feed { get; set; } = "feed-1";

        /// <summary>
        /// The number of streams that have been opened.
        /// </summary>
        public int ConnectCount
        {
            get { lock (gate) { return connectCount; } }
        }

        /// <summary>
        /// The number of times the feed has been registered.
        /// </summary>
        public int FeedsCallCount
        {
            get { lock (gate) { return feedsCallCount; } }
        }

        /// <summary>
        /// The bookmark each stream asked to resume from, in the order the streams opened.
        /// </summary>
        public ImmutableList<string> RequestedBookmarks
        {
            get { lock (gate) { return requestedBookmarks.ToImmutableList(); } }
        }

        /// <summary>
        /// Forgets the feed registration, as a replicator does when it restarts or reloads its
        /// policy. Until <see cref="Feeds"/> registers it again, every stream opened on it
        /// reports <see cref="FeedNotFoundException"/>, and so does the stream that is open.
        /// </summary>
        public void LoseRegistration()
        {
            lock (gate)
            {
                registered = false;
            }
        }

        /// <summary>
        /// When set, <see cref="Feeds"/> does not complete until this task does, so a test can
        /// hold a registration in flight while it raises further errors.
        /// </summary>
        public Task FeedsGate { get; set; }

        /// <summary>
        /// When set, the next declaration is cancelled rather than answered, as one made with a
        /// subscriber's connection token is when the refresh timer cancels that token. The flag
        /// clears itself, so the declaration after it is answered.
        /// </summary>
        public bool CancelNextDeclaration { get; set; }

        /// <summary>
        /// The references the next response delivers. <see cref="ResponseGraph"/> has to carry
        /// facts for each of them, because that is what <see cref="Load"/> answers with.
        /// </summary>
        public ImmutableList<FactReference> ResponseReferences { get; set; } = ImmutableList<FactReference>.Empty;

        public FactGraph ResponseGraph { get; set; } = FactGraph.Empty;

        /// <summary>
        /// Reports an error on the stream that is open. It can be called repeatedly, because
        /// a backoff tick that reconnects to a feed the replicator has forgotten reports the
        /// same error again.
        /// </summary>
        public void RaiseErrorOnLatestConnection(Exception ex)
        {
            Action<Exception> onError;
            lock (gate)
            {
                onError = currentErrorCallback;
            }
            onError?.Invoke(ex);
        }

        /// <summary>
        /// Completes once at least <paramref name="count"/> streams have been opened.
        /// </summary>
        public Task ConnectionReached(int count) => Reached(connectWaiters, count, () => connectCount);

        /// <summary>
        /// Completes once the feed has been registered at least <paramref name="count"/> times.
        /// </summary>
        public Task FeedsReached(int count) => Reached(feedsWaiters, count, () => feedsCallCount);

        private Task Reached(List<(int threshold, TaskCompletionSource<bool> waiter)> waiters, int count, Func<int> current)
        {
            lock (gate)
            {
                if (current() >= count)
                {
                    return Task.CompletedTask;
                }
                var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                waiters.Add((count, waiter));
                return waiter.Task;
            }
        }

        private static void Release(List<(int threshold, TaskCompletionSource<bool> waiter)> waiters, int reached)
        {
            // Called while holding the lock. Completing the task is safe here because the
            // continuations run asynchronously.
            foreach (var released in waiters.Where(w => w.threshold <= reached).ToList())
            {
                released.waiter.SetResult(true);
            }
            waiters.RemoveAll(w => w.threshold <= reached);
        }

        public Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<ImmutableList<string>> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            Task feedsGate = FeedsGate;
            bool cancel;
            lock (gate)
            {
                feedsCallCount++;
                cancel = CancelNextDeclaration;
                CancelNextDeclaration = false;
                Release(feedsWaiters, feedsCallCount);
            }
            if (cancel)
            {
                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                cancelled.Token.ThrowIfCancellationRequested();
            }
            if (feedsGate != null)
            {
                await feedsGate.ConfigureAwait(false);
            }
            lock (gate)
            {
                registered = true;
            }
            return ImmutableList.Create(Feed);
        }

        public Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
        {
            return Task.FromResult((ImmutableList<FactReference>.Empty, bookmark));
        }

        public void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError)
        {
            int count;
            bool known;
            lock (gate)
            {
                count = ++connectCount;
                known = registered;
                requestedBookmarks.Add(bookmark);
                currentErrorCallback = onError;
                Release(connectWaiters, count);
            }

            if (!known)
            {
                onError(new FeedNotFoundException(feed));
                return;
            }

            _ = onResponse(ResponseReferences, "bookmark-" + count);
        }

        public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken)
        {
            return Task.FromResult(ResponseGraph);
        }

        public Task Save(FactGraph graph, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
