using Xunit;
using Jinaga.Http;
using FluentAssertions;
using System;
using System.Text;
using System.IO;

namespace Jinaga.Test.Http;

public class GraphDeserializerTests
{
    [Fact]
    public void GraphDeserializer_Empty()
    {
        var deserializer = new GraphDeserializer();

        deserializer.Facts.Should().BeEmpty();
    }

    [Fact]
    public void GraphDeserializer_OneFact()
    {
        var deserializer = new GraphDeserializer();

        WhenDeserialize(deserializer, "\"MyApp.Root\"\n{}\n{\"identifier\":\"root\"}\n\n");

        deserializer.Facts.Should().HaveCount(1);
    }

    [Fact]
    public void GraphDeserializer_TwoFacts()
    {
        var deserializer = new GraphDeserializer();

        WhenDeserialize(deserializer, "\"MyApp.Root\"\n{}\n{}\n\n\"MyApp.Child\"\n{\"root\":0}\n{}\n\n");

        deserializer.Facts.Should().HaveCount(2);
    }

    private void WhenDeserialize(GraphDeserializer deserializer, string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        deserializer.Deserialize(stream);
    }
}