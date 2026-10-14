using System.Collections.Generic;
using System.Linq;

namespace Jinaga.Test.Specifications;

/// <summary>
/// A specification that projects <c>j.Hash</c> and walks only predecessors reports
/// <c>CanRunOnGraph</c>, so it is evaluated from the fact graph without visiting the
/// store: by <c>ObservableSource.NotifyFactSaved</c> when a watch is notified of a
/// local save, and by the query path when the given is already in the graph.
/// </summary>
public class HashProjectionOnGraphTest
{
    [Fact]
    public async Task HashProjection_WatchReceivesTheHashOfALocallySavedFact()
    {
        var j = JinagaClient.Create();
        var creator = await j.Fact(new User("--- PUBLIC KEY ---"));
        var site = await j.Fact(new Model.Site(creator, "michaelperry.net"));

        var added = new List<ContentHash>();
        var watch = j.Watch(Given<Model.Site>.Match((s, facts) =>
            from c in facts.OfType<Model.Content>()
            where c.site == s
            select new ContentHash(c, j.Hash(c))
        ), site, added.Add);

        try
        {
            await watch.Loaded;

            var content = new Model.Content(site, "/a");
            await j.Fact(content);

            var result = added.Should().ContainSingle().Subject;
            result.content.Should().Be(content);
            result.hash.Should().Be(j.Hash(content));
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task HashProjection_PredecessorQueryReturnsTheSameHashAsTheStorePath()
    {
        var j = JinagaClient.Create();
        var creator = await j.Fact(new User("--- PUBLIC KEY ---"));
        var site = await j.Fact(new Model.Site(creator, "michaelperry.net"));
        var content = await j.Fact(new Model.Content(site, "/a"));

        // Walks only predecessors from the given, which is in the graph, so this
        // specification is evaluated without visiting the store.
        var sitesOfContent = Given<Model.Content>.Match((c, facts) =>
            from s in facts.OfType<Model.Site>()
            where c.site == s
            select new SiteHash(s, j.Hash(s))
        );
        sitesOfContent.CanRunOnGraph.Should().BeTrue();

        var fromGraph = await j.Query(sitesOfContent, content);

        // The store path reaches the same projection through a successor walk, so the
        // expected hash is read back out of the library rather than written as a literal.
        var sitesOfContentThroughTheStore = Given<Model.Site>.Match((s, facts) =>
            from c in facts.OfType<Model.Content>()
            where c.site == s
            select new SiteHash(s, j.Hash(s))
        );
        sitesOfContentThroughTheStore.CanRunOnGraph.Should().BeFalse();
        var fromStore = await j.Query(sitesOfContentThroughTheStore, site);

        var expectedHash = fromStore.Should().ContainSingle().Subject.hash;
        var projected = fromGraph.Should().ContainSingle().Subject;
        projected.hash.Should().Be(expectedHash);
        projected.hash.Should().Be(j.Hash(site));
        projected.site.Should().Be(site);
    }

    record ContentHash(Model.Content content, string hash);

    record SiteHash(Model.Site site, string hash);
}
