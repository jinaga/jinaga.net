using Xunit;
using Jinaga.Http;
using FluentAssertions;
using System;
using System.Text;
using System.IO;
using System.Linq;
using Jinaga.Facts;

namespace Jinaga.Test.Http;

public class GraphDeserializerTests
{
    [Fact]
    public void GraphDeserializer_Empty()
    {
        var deserializer = new GraphDeserializer();

        deserializer.Graph.FactReferences.Should().BeEmpty();
    }

    [Fact]
    public void GraphDeserializer_OneFact()
    {
        var deserializer = new GraphDeserializer();

        WhenDeserialize(deserializer, "\"MyApp.Root\"\n{}\n{\"identifier\":\"root\"}\n\n");

        deserializer.Graph.FactReferences.Should().HaveCount(1);

        var root = deserializer.Graph.GetFact(deserializer.Graph.FactReferences[0]);
        root.Reference.Type.Should().Be("MyApp.Root");
        root.Predecessors.Should().BeEmpty();
        root.Fields.Should().HaveCount(1);
        root.Fields.Where(f => f.Name == "identifier").Should().ContainSingle().Subject
            .Value.StringValue.Should().Be("root");
    }

    [Fact]
    public void GraphDeserializer_TwoFacts()
    {
        var deserializer = new GraphDeserializer();

        WhenDeserialize(deserializer, "\"MyApp.Root\"\n{}\n{}\n\n\"MyApp.Child\"\n{\"root\":0}\n{}\n\n");

        deserializer.Graph.FactReferences.Should().HaveCount(2);

        var root = deserializer.Graph.GetFact(deserializer.Graph.FactReferences[0]);
        root.Reference.Type.Should().Be("MyApp.Root");
        root.Predecessors.Should().BeEmpty();
        root.Fields.Should().BeEmpty();

        var child = deserializer.Graph.GetFact(deserializer.Graph.FactReferences[1]);
        child.Reference.Type.Should().Be("MyApp.Child");
        child.Predecessors.Should().HaveCount(1);
        child.Predecessors.Where(p => p.Role == "root").Should().ContainSingle().Subject
            .Should().BeOfType<PredecessorSingle>().Subject
            .Reference.Should().Be(root.Reference);
        child.Fields.Should().BeEmpty();
    }

    [Fact]
    public void GraphDeserializer_WithSignatures()
    {
        var deserializer = new GraphDeserializer();

        WhenDeserialize(deserializer,
            "PK0\n\"public\"\n\n" +
            "\"MyApp.Root\"\n{}\n{}\nPK0\n\"signature\"\n\n" +
            "PK1\n\"public2\"\n\n" +
            "\"MyApp.Child\"\n{\"root\":0}\n{}\nPK0\n\"signature1\"\nPK1\n\"signature2\"\n\n");

        deserializer.Graph.FactReferences.Should().HaveCount(2);

        var root = deserializer.Graph.GetFact(deserializer.Graph.FactReferences[0]);
        root.Reference.Type.Should().Be("MyApp.Root");
        root.Predecessors.Should().BeEmpty();
        root.Fields.Should().BeEmpty();

        var child = deserializer.Graph.GetFact(deserializer.Graph.FactReferences[1]);
        child.Reference.Type.Should().Be("MyApp.Child");
        child.Predecessors.Should().HaveCount(1);
        child.Predecessors.Where(p => p.Role == "root").Should().ContainSingle().Subject
            .Should().BeOfType<PredecessorSingle>().Subject
            .Reference.Should().Be(root.Reference);
        child.Fields.Should().BeEmpty();
    }

    private void WhenDeserialize(GraphDeserializer deserializer, string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        deserializer.Deserialize(stream);
    }
}