using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jinaga.UnitTest;
using Xunit;

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
                hash = jinagaClient.Hash(content)
            }
        );
        var contentPath = await jinagaClient.Query(contentPathForSite, site);

        contentPath.Should().ContainSingle().Which.path.Should().Be("index.html");
        contentPath.Should().ContainSingle().Which.hash.Should().Be(originalHash);
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
                hash = jinagaClient.Hash(content)
            }
        );
        var contentCreatedAt = await jinagaClient.Query(contentCreatedAtForSite, site);

        contentCreatedAt.Should().ContainSingle().Which.createdAt.Should().BeNull();
        contentCreatedAt.Should().ContainSingle().Which.hash.Should().Be(originalHash);
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
                hash = jinagaClient.Hash(content)
            }
        );
        var contentCreatedAt = await jinagaClient.Query(contentCreatedAtForSite, site);

        contentCreatedAt.Should().ContainSingle().Which.createdAt.Should().Be(new DateTime(2021, 1, 1).ToUniversalTime());
        contentCreatedAt.Should().ContainSingle().Which.hash.Should().Be(originalHash);
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
                hash = jinagaClient.Hash(content)
            }
        );
        var contentPath = await jinagaClient.Query(contentPathForSite, site);

        contentPath.Should().ContainSingle().Which.path.Should().Be("");
        contentPath.Should().ContainSingle().Which.hash.Should().Be(originalHash);
    }
}
