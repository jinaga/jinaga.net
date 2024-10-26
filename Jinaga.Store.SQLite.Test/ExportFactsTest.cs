using Jinaga.Store.SQLite.Test.Models;
using System.Text.Json;
using Xunit;

namespace Jinaga.Store.SQLite.Test
{
    public class ExportFactsTest
    {
        [Fact]
        public async Task ExportFactsToJson_ShouldReturnJsonString()
        {
            // Arrange
            var store = new SQLiteStore("test.db", new NullLoggerFactory());
            var jinagaClient = new JinagaClient(store, new LocalNetwork(), new NullLoggerFactory());

            var site = await jinagaClient.Fact(new Site("qedcode.com"));
            var post = await jinagaClient.Fact(new Post(site, "2022-08-16T15:23:13.231Z"));
            var title1 = await jinagaClient.Fact(new Title(post, "Introducing Jinaga Replicator", new Title[0]));
            var title2 = await jinagaClient.Fact(new Title(post, "Introduction to the Jinaga Replicator", new[] { title1 }));

            // Act
            var json = await jinagaClient.Internal.ExportFactsToJson();

            // Assert
            var facts = JsonSerializer.Deserialize<List<Fact>>(json);
            Assert.NotNull(facts);
            Assert.Equal(4, facts.Count);
        }

        [Fact]
        public async Task ExportFactsToFactual_ShouldReturnFactualString()
        {
            // Arrange
            var store = new SQLiteStore("test.db", new NullLoggerFactory());
            var jinagaClient = new JinagaClient(store, new LocalNetwork(), new NullLoggerFactory());

            var site = await jinagaClient.Fact(new Site("qedcode.com"));
            var post = await jinagaClient.Fact(new Post(site, "2022-08-16T15:23:13.231Z"));
            var title1 = await jinagaClient.Fact(new Title(post, "Introducing Jinaga Replicator", new Title[0]));
            var title2 = await jinagaClient.Fact(new Title(post, "Introduction to the Jinaga Replicator", new[] { title1 }));

            // Act
            var factual = await jinagaClient.Internal.ExportFactsToFactual();

            // Assert
            Assert.Contains("let f1: Blog.Site = {", factual);
            Assert.Contains("let f2: Blog.Post = {", factual);
            Assert.Contains("let f3: Blog.Post.Title = {", factual);
            Assert.Contains("let f4: Blog.Post.Title = {", factual);
        }
    }
}
