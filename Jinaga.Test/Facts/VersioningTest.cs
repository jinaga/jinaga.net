using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Serialization;
using Xunit;

namespace Jinaga.Test.Facts;

public class VersioningTest
{
    [Fact]
    public void CanStayOnVersionOne()
    {
        var original = new BlogV1("michaelperry.net");
        var factGraph = Serialize(original);
        var deserialized = Deserialize<BlogV1>(factGraph, factGraph.Last);

        deserialized.domain.Should().Be("michaelperry.net");
    }

    [Fact]
    public void CanUpgradeWithStringField()
    {
        var original = new BlogV1("michaelperry.net");
        var factGraph = Serialize(original);
        var deserialized = Deserialize<BlogV2>(factGraph, factGraph.Last);

        deserialized.domain.Should().Be("michaelperry.net");
        deserialized.title.Should().Be("");
    }

    private static FactGraph Serialize(object fact)
    {
        var serializerCache = SerializerCache.Empty;
        var collector = new Collector(serializerCache);
        collector.Serialize(fact);
        serializerCache = collector.SerializerCache;
        return collector.Graph;
    }

    private static T Deserialize<T>(FactGraph graph, FactReference reference)
    {
        var emitter = new Emitter(graph, DeserializerCache.Empty);
        var runtimeFact = emitter.Deserialize<T>(reference);
        return runtimeFact;
    }
}

[FactType("Blog")]
internal record BlogV1(string domain) {}

[FactType("Blog")]
internal record BlogV2(string domain, string title) {}