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
    public async Task CanReadVersionTwoFieldFromVersionOneFact()
    {
        var jinagaClient = JinagaTest.Create();
        var site = await jinagaClient.Fact(new Model.Site(new User("Michael"), "michaelperry.net"));
        var contentV1 = await jinagaClient.Fact(new Model.Content(site, "index.html"));

        var contentCreatedAtForSite = Given<Model.Site>.Match((site, facts) =>
            from content in facts.OfType<Model.ContentV2>()
            where content.site == site
            select content.createdAt
        );
        var contentCreatedAt = await jinagaClient.Query(contentCreatedAtForSite, site);

        contentCreatedAt.Should().ContainSingle().Which.Should().BeNull();
    }

    [Fact]
    public async Task CanReadVersionTwoFieldFromVersionTwoFact()
    {
        var jinagaClient = JinagaTest.Create();
        var site = await jinagaClient.Fact(new Model.Site(new User("Michael"), "michaelperry.net"));
        var contentV2 = await jinagaClient.Fact(new Model.ContentV2(site, new DateTime(2021, 1, 1).ToUniversalTime()));

        var contentCreatedAtForSite = Given<Model.Site>.Match((site, facts) =>
            from content in facts.OfType<Model.ContentV2>()
            where content.site == site
            select content.createdAt
        );
        var contentCreatedAt = await jinagaClient.Query(contentCreatedAtForSite, site);

        contentCreatedAt.Should().ContainSingle().Which.Should().Be(new DateTime(2021, 1, 1).ToUniversalTime());
    }

    [Fact]
    public async Task CanReadVersionOneFieldFromVersionTwoFact()
    {
        var jinagaClient = JinagaTest.Create();
        var site = await jinagaClient.Fact(new Model.Site(new User("Michael"), "michaelperry.net"));
        var contentV2 = await jinagaClient.Fact(new Model.ContentV2(site, new DateTime(2021, 1, 1)));

        var contentPathForSite = Given<Model.Site>.Match((site, facts) =>
            from content in facts.OfType<Model.Content>()
            where content.site == site
            select content.path
        );
        var contentPath = await jinagaClient.Query(contentPathForSite, site);

        contentPath.Should().ContainSingle().Which.Should().Be("");
    }
}
