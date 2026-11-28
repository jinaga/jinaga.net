using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System.Collections.Immutable;

namespace Jinaga.Store.SQLite.Test.Fakes;

/// <summary>
/// A network that fails every call, which is what an offline device looks like. A client
/// built on one of these can still read its own store, so anything that answers without
/// reaching the replicator answers here too.
/// </summary>
internal class FailingFakeNetwork : INetwork
{
    public int SaveCallCount { get; private set; } = 0;

#pragma warning disable CS0067 // Event is never used; required to satisfy INetwork
    public event INetwork.AuthenticationStateChanged? OnAuthenticationStateChanged;
#pragma warning restore CS0067

    public Task<(ImmutableList<string> feeds, ImmutableList<FeedDecision> decisions)> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Simulated network failure: endpoint unreachable.");

    public Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Simulated network failure: endpoint unreachable.");

    public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Simulated network failure: endpoint unreachable.");

    public Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Simulated network failure: endpoint unreachable.");

    public Task Save(FactGraph graph, CancellationToken cancellationToken)
    {
        SaveCallCount++;
        throw new InvalidOperationException("Simulated network failure: endpoint unreachable.");
    }

    public void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError) =>
        throw new InvalidOperationException("Simulated network failure: endpoint unreachable.");
}
