using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Extensions;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Jinaga.Test.Specifications;

// Jinaga.Test declares its own Site, and a member of an enclosing namespace wins over a
// using directive, so name these deliberately.
using Comment = Jinaga.Test.Model.Comment;
using Content = Jinaga.Test.Model.Content;
using Publish = Jinaga.Test.Model.Publish;
using Site = Jinaga.Test.Model.Site;

public record ContentWithPath(Content content, string path) { }

public record ContentWithComments(Content content, IEnumerable<Comment> comments) { }

public record ContentWithPublishes(Content content, IEnumerable<Publish> publishes) { }

/// <summary>
/// A feed registration means "these givens, these matches, these collections". Two
/// specifications that agree on all three declare the same feeds, so the client declares
/// them to the replicator once between them.
/// </summary>
public class FeedDeclarationTest
{
    private readonly ITestOutputHelper output;

    public FeedDeclarationTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    private static readonly Specification<Site, Content> bareContent =
        Given<Site>.Match((site, facts) =>
            from content in facts.OfType<Content>()
            where content.site == site
            select content
        );

    private static readonly Specification<Site, ContentWithPath> contentWithPath =
        Given<Site>.Match((site, facts) =>
            from content in facts.OfType<Content>()
            where content.site == site
            select new ContentWithPath(content, content.path)
        );

    private static readonly Specification<Site, ContentWithComments> contentWithComments =
        Given<Site>.Match((site, facts) =>
            from content in facts.OfType<Content>()
            where content.site == site
            select new ContentWithComments(
                content,
                facts.OfType<Comment>(comment => comment.content == content)
            )
        );

    private static readonly Specification<Site, ContentWithPublishes> contentWithPublishes =
        Given<Site>.Match((site, facts) =>
            from content in facts.OfType<Content>()
            where content.site == site
            select new ContentWithPublishes(
                content,
                facts.OfType<Publish>(publish => publish.content == content)
            )
        );

    [Fact]
    public async Task Query_DeclaresOneFeedForTwoProjectionsOfTheSameMatches()
    {
        var network = new CountingFakeNetwork(output);
        var j = GivenJinagaClient(network);
        var site = await GivenSite(j);

        await j.Query(bareContent, site);
        await j.Query(contentWithPath, site);

        network.FeedsCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Watch_DeclaresOneFeedForTwoProjectionsOfTheSameMatches()
    {
        var network = new CountingFakeNetwork(output);
        var j = GivenJinagaClient(network);
        var site = await GivenSite(j);

        var bareObserver = j.Watch(bareContent, site, content => { });
        await bareObserver.Loaded;
        var withPathObserver = j.Watch(contentWithPath, site, projection => { });
        await withPathObserver.Loaded;

        network.FeedsCallCount.Should().Be(1);

        bareObserver.Stop();
        withPathObserver.Stop();
    }

    /// <summary>
    /// The feed tests above declare <c>ContentWithComments</c> without ever reading it back,
    /// so nothing here deserialized its <c>IEnumerable&lt;Comment&gt;</c>. Query it through a
    /// client so the record the suite already declares is also materialized.
    /// </summary>
    [Fact]
    public async Task Query_FillsTheCommentsOfContentWithComments()
    {
        var j = JinagaTest.Create();
        var creator = await j.Fact(new User("--- PUBLIC KEY ---"));
        var site = await j.Fact(new Site(creator, "contoso"));
        var content = await j.Fact(new Content(site, "/first-post"));
        var comment = await j.Fact(new Comment(content, System.Guid.NewGuid(), creator));

        var results = await j.Query(contentWithComments, site);

        var result = results.Should().ContainSingle().Subject;
        result.content.Should().BeEquivalentTo(content);
        result.comments.Should().BeEquivalentTo(new[] { comment });
    }

    [Fact]
    public async Task Query_DeclaresSeparateFeedsForDifferentCollections()
    {
        var network = new CountingFakeNetwork(output);
        var j = GivenJinagaClient(network);
        var site = await GivenSite(j);

        await j.Query(contentWithComments, site);
        await j.Query(contentWithPublishes, site);

        network.FeedsCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Watch_DeclaresSeparateFeedsForDifferentCollections()
    {
        var network = new CountingFakeNetwork(output);
        var j = GivenJinagaClient(network);
        var site = await GivenSite(j);

        var withCommentsObserver = j.Watch(contentWithComments, site, projection => { });
        await withCommentsObserver.Loaded;
        var withPublishesObserver = j.Watch(contentWithPublishes, site, projection => { });
        await withPublishesObserver.Loaded;

        network.FeedsCallCount.Should().Be(2);

        withCommentsObserver.Stop();
        withPublishesObserver.Stop();
    }

    private static JinagaClient GivenJinagaClient(Jinaga.Services.INetwork network)
    {
        return new JinagaClient(
            new MemoryStore(),
            network,
            ImmutableList<Jinaga.Projections.Specification>.Empty,
            NullLoggerFactory.Instance,
            new JinagaClientOptions());
    }

    private static async Task<Site> GivenSite(JinagaClient j)
    {
        var creator = await j.Fact(new User("--- PUBLIC KEY ---"));
        return await j.Fact(new Site(creator, "contoso"));
    }
}
