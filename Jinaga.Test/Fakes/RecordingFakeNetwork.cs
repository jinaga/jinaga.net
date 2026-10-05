using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Test.Fakes
{
    /// <summary>
    /// A fake network that records the graph of every Save it accepts, and can
    /// be told to fail a particular Save. Used to observe how the outbound
    /// queue is divided into requests.
    /// </summary>
    internal class RecordingFakeNetwork : INetwork
    {
        private readonly List<FactGraph> savedGraphs = new List<FactGraph>();
        private readonly HashSet<int> failingSaveCalls = new HashSet<int>();
        private int saveCallCount = 0;

#pragma warning disable CS0067 // Event is never used; required to satisfy INetwork
        public event INetwork.AuthenticationStateChanged OnAuthenticationStateChanged;
#pragma warning restore CS0067

        /// <summary>
        /// The graphs of the Save calls that succeeded, in order.
        /// </summary>
        public IReadOnlyList<FactGraph> SavedGraphs => savedGraphs;

        /// <summary>
        /// Causes the Save call at the given position, counted from one over
        /// the life of this fake, to throw instead of recording its graph.
        /// </summary>
        public void FailSaveCall(int callNumber)
        {
            failingSaveCalls.Add(callNumber);
        }

        public Task Save(FactGraph graph, CancellationToken cancellationToken)
        {
            saveCallCount++;
            if (failingSaveCalls.Contains(saveCallCount))
            {
                throw new InvalidOperationException($"Simulated failure of save call {saveCallCount}.");
            }
            savedGraphs.Add(graph);
            return Task.CompletedTask;
        }

        public Task<ImmutableList<string>> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError)
        {
            throw new NotImplementedException();
        }
    }
}
