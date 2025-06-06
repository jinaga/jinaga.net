using System.Linq;

namespace Jinaga.Test.Specifications;

public class ProjectionVersioningTest
{
    [Fact]
    public async Task CanReadVersionOneFieldFromVersionOneFact()
    {
        var jinagaClient = JinagaTest.Create();
        var site = await jinagaClient.Fact(new Model.Site(new User("Michael"), "michaelperry.net"));
        var content = await jinagaClient.Fact(new Model.Content(site, "index.html"));
        var originalHash = jinagaClient.Hash(content);

        var contentPathForSite = Given<Model.Site>.Match((site, facts) =>
            from content in facts.OfType<Model.Content>()
            where content.site == site
            select new
            {
                content.path,
                hash = jinagaClient.Hash(content),
                content
            }
        );
        var contentPath = await jinagaClient.Query(contentPathForSite, site);

        var projection = contentPath.Should().ContainSingle().Subject;
        projection.path.Should().Be("index.html");
        projection.hash.Should().Be(originalHash);
        jinagaClient.Hash(projection.content).Should().Be(originalHash);
    }

    [Fact]
    public async Task CanReadVersionTwoFieldFromVersionOneFact()
    {
        var jinagaClient = JinagaTest.Create();
        var site = await jinagaClient.Fact(new Model.Site(new User("Michael"), "michaelperry.net"));
        var contentV1 = await jinagaClient.Fact(new Model.Content(site, "index.html"));
        var originalHash = jinagaClient.Hash(contentV1);

        var contentCreatedAtForSite = Given<Model.Site>.Match((site, facts) =>
            from content in facts.OfType<Model.ContentV2>()
            where content.site == site
            select new
            {
                content.createdAt,
                hash = jinagaClient.Hash(content),
                content
            }
        );
        var contentCreatedAt = await jinagaClient.Query(contentCreatedAtForSite, site);

        var projection = contentCreatedAt.Should().ContainSingle().Subject;
        projection.createdAt.Should().BeNull();
        projection.hash.Should().Be(originalHash);
        jinagaClient.Hash(projection.content).Should().Be(originalHash);
    }

    [Fact]
    public async Task CanReadVersionTwoFieldFromVersionTwoFact()
    {
        var jinagaClient = JinagaTest.Create();
        var site = await jinagaClient.Fact(new Model.Site(new User("Michael"), "michaelperry.net"));
        var contentV2 = await jinagaClient.Fact(new Model.ContentV2(site, new DateTime(2021, 1, 1).ToUniversalTime()));
        var originalHash = jinagaClient.Hash(contentV2);

        var contentCreatedAtForSite = Given<Model.Site>.Match((site, facts) =>
            from content in facts.OfType<Model.ContentV2>()
            where content.site == site
            select new
            {
                content.createdAt,
                hash = jinagaClient.Hash(content),
                content
            }
        );
        var contentCreatedAt = await jinagaClient.Query(contentCreatedAtForSite, site);

        var projection = contentCreatedAt.Should().ContainSingle().Subject;
        projection.createdAt.Should().Be(new DateTime(2021, 1, 1).ToUniversalTime());
        projection.hash.Should().Be(originalHash);
        jinagaClient.Hash(projection.content).Should().Be(originalHash);
    }

    [Fact]
    public async Task CanReadVersionOneFieldFromVersionTwoFact()
    {
        var jinagaClient = JinagaTest.Create();
        var site = await jinagaClient.Fact(new Model.Site(new User("Michael"), "michaelperry.net"));
        var contentV2 = await jinagaClient.Fact(new Model.ContentV2(site, new DateTime(2021, 1, 1)));
        var originalHash = jinagaClient.Hash(contentV2);

        var contentPathForSite = Given<Model.Site>.Match((site, facts) =>
            from content in facts.OfType<Model.Content>()
            where content.site == site
            select new
            {
                content.path,
                hash = jinagaClient.Hash(content),
                content
            }
        );
        var contentPath = await jinagaClient.Query(contentPathForSite, site);

        var projection = contentPath.Should().ContainSingle().Subject;
        projection.path.Should().Be("");
        projection.hash.Should().Be(originalHash);
        jinagaClient.Hash(projection.content).Should().Be(originalHash);
    }
}
