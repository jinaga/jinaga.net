using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System.Collections.Immutable;

namespace Jinaga.Maui.Test.Fakes;

/// <summary>
/// A replicator that accepts every upload and serves nothing, so a test can see what the
/// client sent.
/// </summary>
internal class UploadRecordingNetwork : INetwork
{
#pragma warning disable CS0067
    public event INetwork.AuthenticationStateChanged? OnAuthenticationStateChanged;
#pragma warning restore CS0067

    /// <summary>
    /// Everything that has been uploaded, merged into one graph.
    /// </summary>
    public FactGraph UploadedGraph { get; private set; } = FactGraph.Empty;

    public Task Save(FactGraph graph, CancellationToken cancellationToken)
    {
        UploadedGraph = UploadedGraph.AddGraph(graph);
        return Task.CompletedTask;
    }

    public Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ImmutableList<string>> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken) =>
        Task.FromResult(ImmutableList<string>.Empty);

    public Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken) =>
        Task.FromResult((ImmutableList<FactReference>.Empty, bookmark));

    public void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError)
    {
    }

    public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken) =>
        Task.FromResult(FactGraph.Empty);
}
