using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Serialization;
using Jinaga.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;

namespace Jinaga.Test.Fakes;
internal class FakeFeed
{
    public string Name { get; set; }
    public Fact[] Facts { get; set; }
    public int Delay { get; set; }
}
internal class FakeNetwork : INetwork
{
    private SerializerCache serializerCache = SerializerCache.Empty;
    private ITestOutputHelper output;
    private readonly List<FakeFeed> feeds = new();
    private readonly Dictionary<FactReference, Fact> factByFactReference = new();
    private string finalBookmark = "done";

    public ImmutableList<Fact> UploadedFacts { get; private set; } = ImmutableList<Fact>.Empty;

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
            if (!factByFactReference.ContainsKey(fact.Reference))
            {
                factByFactReference.Add(fact.Reference, fact);
            }
        }

        // If the network already has the feed, then replace it.
        // Bump the bookmark to force a reload.
        var existingFeed = feeds.SingleOrDefault(f => f.Name == name);
        if (existingFeed != null)
        {
            feeds.Remove(existingFeed);
            finalBookmark = Guid.NewGuid().ToString();
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
        if (bookmark == finalBookmark)
        {
            return (ImmutableList<FactReference>.Empty, finalBookmark);
        }
        var fakeFeed = feeds.Single(f => f.Name == feed);
        var references = fakeFeed.Facts
            .Select(fact => fact.Reference)
            .ToImmutableList();
        if (fakeFeed.Delay > 0)
        {
            await Task.Delay(fakeFeed.Delay);
        }
        return (references, finalBookmark);
    }

    public void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError)
    {
        if (bookmark == finalBookmark)
        {
            onResponse(ImmutableList<FactReference>.Empty, finalBookmark);
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
                await onResponse(references, finalBookmark);
            });
        }
        else
        {
            onResponse(references, finalBookmark);
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
        var fact = factByFactReference[factReference];
        foreach (var predecessor in fact.Predecessors)
        {
            foreach (var predecessorReference in predecessor.AllReferences)
            {
                graph = AddFact(graph, predecessorReference);
            }
        }
        graph = graph.Add(fact);
        return graph;
    }

    public Task Save(ImmutableList<Fact> facts, CancellationToken cancellationToken)
    {
        UploadedFacts = UploadedFacts.AddRange(facts);
        return Task.CompletedTask;
    }
}
