using Jinaga.Facts;
using Jinaga.Managers;
using Jinaga.Projections;
using Jinaga.Serialization;
using Jinaga.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Test.Fakes;
internal class FakeFeed
{
    public string Name { get; set; }
    public Fact[] Facts { get; set; }
}
internal class FakeNetwork : INetwork
{
    private SerializerCache serializerCache = SerializerCache.Empty;

    private readonly List<FakeFeed> feeds = new();
    private readonly Dictionary<FactReference, Fact> factByFactReference = new();

    public void AddFeed(string name, object[] facts)
    {
        var collector = new Collector(serializerCache);
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
            factByFactReference.Add(fact.Reference, fact);
        }
        feeds.Add(new FakeFeed
        {
            Name = name,
            Facts = serializedFacts
        });
    }

    public Task<ImmutableList<string>> Feeds(ImmutableList<FactReference> givenReferences, Specification specification, CancellationToken cancellationToken)
    {
        return Task.FromResult(feeds.Select(feed => feed.Name).ToImmutableList());
    }

    public Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
    {
        if (bookmark == "done")
        {
            return Task.FromResult((ImmutableList<FactReference>.Empty, "done"));
        }
        var fakeFeed = feeds.Single(f => f.Name == feed);
        var references = fakeFeed.Facts
            .Select(fact => fact.Reference)
            .ToImmutableList();
        return Task.FromResult((references, "done"));
    }

    public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken)
    {
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
        throw new NotImplementedException();
    }
}
