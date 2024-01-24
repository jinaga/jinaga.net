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
    public void CanSerializeNullableDateTimeFieldWithValue()
    {
        var original = new BlogV4(new DateTime(2021, 1, 1).ToUniversalTime());
        var factGraph = Serialize(original);
        var deserialized = Deserialize<BlogV4>(factGraph, factGraph.Last);

        deserialized.createdAt.Should().Be(new DateTime(2021, 1, 1).ToUniversalTime());
    }

    [Fact]
    public void CanSerializeNullableDateTimeFieldWithNull()
    {
        var original = new BlogV4(null);
        var factGraph = Serialize(original);
        var deserialized = Deserialize<BlogV4>(factGraph, factGraph.Last);

        deserialized.createdAt.Should().BeNull();
    }

    [Fact]
    public void CanUpgradeNullableDateTimeToNonNullable()
    {
        var original = new BlogV4(null);
        var factGraph = Serialize(original);
        var deserialized = Deserialize<BlogV5>(factGraph, factGraph.Last);

        deserialized.createdAt.Should().Be(DateTime.UnixEpoch);
    }

    [Fact]
    public void CanDowngrade()
    {
        var original = new BlogV3("michaelperry.net", "Michael's Blog", 5);
        var factGraph = Serialize(original);
        var deserialized = Deserialize<BlogV1>(factGraph, factGraph.Last);

        deserialized.domain.Should().Be("michaelperry.net");
    }

    [Fact]
    public void CanAddPredecessor()
    {
        var original = new CommentV1(new BlogV5(DateTime.UtcNow), "Hello, world!");
        var factGraph = Serialize(original);
        var deserialized = Deserialize<CommentV2>(factGraph, factGraph.Last);

        deserialized.message.Should().Be("Hello, world!");
        deserialized.author.Should().BeNull();
    }

    [Fact]
    public void CanRemovePredecessor()
    {
        var original = new CommentV2(new BlogV5(DateTime.UtcNow), "Hello, world!", new User("Michael"));
        var factGraph = Serialize(original);
        var deserialized = Deserialize<CommentV1>(factGraph, factGraph.Last);

        deserialized.message.Should().Be("Hello, world!");
    }

    [Fact]
    public void CanSerializeNullPredecessor()
    {
        var original = new CommentV2(new BlogV5(DateTime.UtcNow), "Hello, world!", null);
        var factGraph = Serialize(original);
        var deserialized = Deserialize<CommentV2>(factGraph, factGraph.Last);

        deserialized.message.Should().Be("Hello, world!");
        deserialized.author.Should().BeNull();
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
public record BlogV1(string domain) {}

[FactType("Blog")]
public record BlogV2(string domain, string title) {}

[FactType("Blog")]
public record BlogV3(string domain, string title, int stars) {}

[FactType("Blog")]
public record BlogV4(DateTime? createdAt) {}

[FactType("Blog")]
public record BlogV5(DateTime createdAt) {}

[FactType("Comment")]
public record CommentV1(BlogV5 blog, string message) {}

[FactType("Comment")]
public record CommentV2(BlogV5 blog, string message, User author) {}