using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Test.Fakes
{
    /// <summary>
    /// A fake network whose feed always returns zero facts, but advances the bookmark
    /// on every call. Used to verify that NetworkManager.ProcessFeed saves the advanced
    /// bookmark even when the response contains no facts.
    /// </summary>
    internal class EmptyResultAdvancingBookmarkNetwork : INetwork
    {
        public int FetchFeedCallCount { get; private set; } = 0;

#pragma warning disable CS0067
        public event INetwork.AuthenticationStateChanged OnAuthenticationStateChanged;
#pragma warning restore CS0067

        public Task<ImmutableList<string>> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            return Task.FromResult(ImmutableList<string>.Empty.Add("feed"));
        }

        public Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
        {
            FetchFeedCallCount++;
            return Task.FromResult((ImmutableList<FactReference>.Empty, $"bookmark-{FetchFeedCallCount}"));
        }

        public void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError)
        {
            throw new NotImplementedException();
        }

        public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken)
        {
            return Task.FromResult(FactGraph.Empty);
        }

        public Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task Save(FactGraph graph, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
