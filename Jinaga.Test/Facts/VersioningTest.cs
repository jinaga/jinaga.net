using System;
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

    [Fact]
    public void CanUpgradeWithIntField()
    {
        var original = new BlogV1("michaelperry.net");
        var factGraph = Serialize(original);
        var deserialized = Deserialize<BlogV3>(factGraph, factGraph.Last);

        deserialized.domain.Should().Be("michaelperry.net");
        deserialized.title.Should().Be("");
        deserialized.stars.Should().Be(0);
    }

    [Fact]
    public void CanUpgradeWithIntFieldAndStringField()
    {
        var original = new BlogV2("michaelperry.net", "Michael's Blog");
        var factGraph = Serialize(original);
        var deserialized = Deserialize<BlogV3>(factGraph, factGraph.Last);

        deserialized.domain.Should().Be("michaelperry.net");
        deserialized.title.Should().Be("Michael's Blog");
        deserialized.stars.Should().Be(0);
    }

    [Fact]
    public void CanUpgradeWithNullableDateTimeField()
    {
        var original = new BlogV1("michaelperry.net");
        var factGraph = Serialize(original);
        var deserialized = Deserialize<BlogV4>(factGraph, factGraph.Last);

        deserialized.createdAt.Should().BeNull();
    }

    [Fact]
    public void CanDowngrade()
    {
        var original = new BlogV3("michaelperry.net", "Michael's Blog", 5);
        var factGraph = Serialize(original);
        var deserialized = Deserialize<BlogV1>(factGraph, factGraph.Last);

        deserialized.domain.Should().Be("michaelperry.net");
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

[FactType("Blog")]
internal record BlogV3(string domain, string title, int stars) {}

[FactType("Blog")]
internal record BlogV4(DateTime? createdAt) {}