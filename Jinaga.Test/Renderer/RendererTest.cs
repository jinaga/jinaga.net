using System.Threading.Tasks;
using FluentAssertions;
using Jinaga.UnitTest;
using Xunit;

namespace Jinaga.Test.Renderer;

public class RendererTest
{
    [Fact]
    public void CanRenderRecord()
    {
        var fact = new User("Public Key");
        var html = Jinaga.Graphviz.Renderer.RenderFacts(fact);
        string htmlString = html.ToString();
        htmlString.Should().Contain(">Jinaga.User<");
        htmlString.Should().Contain(">Public Key<");
    }

    [Fact]
    public async Task CanRenderProxy()
    {
        var jinagaClient = JinagaTest.Create();
        var fact = await jinagaClient.Fact(new User("Public Key"));
        var html = Jinaga.Graphviz.Renderer.RenderFacts(fact);
        string htmlString = html.ToString();
        htmlString.Should().Contain(">Jinaga.User<");
        htmlString.Should().Contain(">Public Key<");
    }

    [Fact]
    public void CanRenderRecordWithPredecessor()
    {
        var site = new Site("michaelperry.net");
        var post = new Post(site, "Hello, world!");
        var html = Jinaga.Graphviz.Renderer.RenderFacts(post);
        string htmlString = html.ToString();
        htmlString.Should().Contain(">Blog.Site<");
        htmlString.Should().Contain(">michaelperry.net<");
        htmlString.Should().Contain(">Blog.Post<");
        htmlString.Should().Contain(">Hello, world!<");
    }

    [Fact]
    public async Task CanRenderProxyWithPredecessor()
    {
        var jinagaClient = JinagaTest.Create();
        var site = await jinagaClient.Fact(new Site("michaelperry.net"));
        var post = await jinagaClient.Fact(new Post(site, "Hello, world!"));
        var html = Jinaga.Graphviz.Renderer.RenderFacts(post);
        string htmlString = html.ToString();
        htmlString.Should().Contain(">Blog.Site<");
        htmlString.Should().Contain(">michaelperry.net<");
        htmlString.Should().Contain(">Blog.Post<");
        htmlString.Should().Contain(">Hello, world!<");
    }
}