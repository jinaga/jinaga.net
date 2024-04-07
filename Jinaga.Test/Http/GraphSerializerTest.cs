using System.Collections.Immutable;
using System.IO;
using Jinaga.Facts;
using Jinaga.Http;

namespace Jinaga.Test.Http;

public class GraphSerializerTest
{
    [Fact]
    public void GraphSerializer_Empty()
    {
        using var stream = new MemoryStream();

        WhenSerializeGraph(stream, FactGraph.Empty);

        ThenOutput(stream).Should().Be("");
    }

    [Fact]
    public void GraphSerializer_OneFact()
    {
        using var stream = new MemoryStream();

        var root = Fact.Create(
            "MyApp.Root",
            ImmutableList.Create(
                new Field("identifier", new FieldValueString("root"))
            ),
            ImmutableList<Predecessor>.Empty
        );
        var graph = FactGraph.Empty.Add(root);

        WhenSerializeGraph(stream, graph);

        ThenOutput(stream).Should().Be("\"MyApp.Root\"\n{}\n{\"identifier\":\"root\"}\n\n");
    }

    [Fact]
    public void GraphSerializer_TwoFacts()
    {
        using var stream = new MemoryStream();

        var root = Fact.Create(
            "MyApp.Root",
            ImmutableList<Field>.Empty,
            ImmutableList<Predecessor>.Empty
        );
        var child = Fact.Create(
            "MyApp.Child",
            ImmutableList<Field>.Empty,
            ImmutableList.Create(
                (Predecessor)new PredecessorSingle("root", root.Reference)
            )
        );
        var graph = FactGraph.Empty
            .Add(root)
            .Add(child);

        WhenSerializeGraph(stream, graph);

        ThenOutput(stream).Should().Be("\"MyApp.Root\"\n{}\n{}\n\n\"MyApp.Child\"\n{\"root\":0}\n{}\n\n");
    }

    private static void WhenSerializeGraph(MemoryStream stream, FactGraph graph)
    {
        using var serializer = new GraphSerializer(stream);
        serializer.Serialize(graph);
    }

    private string ThenOutput(MemoryStream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var output = reader.ReadToEnd();
        return output;
    }
}