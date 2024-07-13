using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Serialization;
using Jinaga.Services;
using System.Collections.Immutable;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test.Fakes;
internal class FakeFeed
{
    public required string Name { get; set; }
    public required Fact[] Facts { get; set; }
    public required int Delay { get; set; }
}
internal class FakeNetwork : INetwork
{
    private SerializerCache serializerCache = SerializerCache.Empty;
    private ITestOutputHelper output;
    private readonly List<FakeFeed> feeds = new();
    private readonly Dictionary<FactReference, FactEnvelope> envelopeByFactReference = new();

#pragma warning disable CS0067
    public event INetwork.AuthenticationStateChanged? OnAuthenticationStateChanged;
#pragma warning restore CS0067

    public FakeNetwork(ITestOutputHelper output)
    {
        this.output = output;
    }

    public void AddFeed(string name, object[] facts, int delay = 0)
    {
        var collector = new Collector(serializerCache, new());
        foreach (var fact in facts)
        {
            collector.Serialize(fact);
        }
        serializerCache = collector.SerializerCache;
        var graph = collector.Graph;
        var serializedFacts = graph.FactReferences
            .Select(graph.GetFact)
            .ToArray();
        foreach (var fact in serializedFacts)
        {
            if (!envelopeByFactReference.ContainsKey(fact.Reference))
            {
                envelopeByFactReference.Add(fact.Reference, new FactEnvelope(fact, ImmutableList<FactSignature>.Empty));
            }
        }
        feeds.Add(new FakeFeed
        {
            Name = name,
            Facts = serializedFacts,
            Delay = delay
        });
    }

    public Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ImmutableList<string>> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
    {
        return Task.FromResult(feeds.Select(feed => feed.Name).ToImmutableList());
    }

    public async Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
    {
        if (bookmark == "done")
        {
            return (ImmutableList<FactReference>.Empty, "done");
        }
        var fakeFeed = feeds.Single(f => f.Name == feed);
        var references = fakeFeed.Facts
            .Select(fact => fact.Reference)
            .ToImmutableList();
        if (fakeFeed.Delay > 0)
        {
            await Task.Delay(fakeFeed.Delay);
        }
        return (references, "done");
    }

    public void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError)
    {
        if (bookmark == "done")
        {
            onResponse(ImmutableList<FactReference>.Empty, "done");
            return;
        }
        var fakeFeed = feeds.Single(f => f.Name == feed);
        var references = fakeFeed.Facts
            .Select(fact => fact.Reference)
            .ToImmutableList();
        if (fakeFeed.Delay > 0)
        {
            Task.Run(async () =>
            {
                await Task.Delay(fakeFeed.Delay);
                await onResponse(references, "done");
            });
        }
        else
        {
            onResponse(references, "done");
        }
    }

    public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken)
    {
        string references = string.Join(",\n", factReferences.Select(r => $"  {r}"));
        output.WriteLine($"Load {factReferences.Count} facts:\n{references}");
        var graph = FactGraph.Empty;

        foreach (var factReference in factReferences)
        {
            graph = AddFact(graph, factReference);
        }

        return Task.FromResult(graph);
    }

    private FactGraph AddFact(FactGraph graph, FactReference factReference)
    {
        var envelope = envelopeByFactReference[factReference];
        foreach (var predecessor in envelope.Fact.Predecessors)
        {
            foreach (var predecessorReference in predecessor.AllReferences)
            {
                graph = AddFact(graph, predecessorReference);
            }
        }
        graph = graph.Add(envelope);
        return graph;
    }

    public Task Save(FactGraph graph, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
