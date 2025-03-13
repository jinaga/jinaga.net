using System.Linq;
using Jinaga.Extensions;

namespace Jinaga.Test.Model;

public class BlogTests
{
    private readonly JinagaClient j;

    public BlogTests()
    {
        j = JinagaTest.Create();
    }

    [Fact]
    public async Task CanQueryForSuccessorsUsingSuccessorsExtension()
    {
        var site = await j.Fact(new Site(new User("--- PUBLIC KEY ---"), "my-blog"));
        var content = await j.Fact(new Content(site, "/first-post"));

        var specification = Given<Site>.Match(site =>
            from content in site.Successors().OfType<Content>(c => c.site)
            select content
        );

        var contents = await j.Query(specification, site);

        contents.Should().ContainSingle().Which.Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task CanQueryForNestedSuccessorsUsingSuccessorsExtension()
    {
        var site = await j.Fact(new Site(new User("--- PUBLIC KEY ---"), "my-blog"));
        var content = await j.Fact(new Content(site, "/first-post"));
        var comment = await j.Fact(new Comment(content, Guid.NewGuid(), new User("--- COMMENTER ---")));

        var specification = Given<Site>.Match(site =>
            from content in site.Successors().OfType<Content>(c => c.site)
            select new
            {
                content,
                comments = content.Successors().OfType<Comment>(comment => comment.content)
            }
        );

        var contents = await j.Query(specification, site);

        var result = contents.Should().ContainSingle().Subject;
        result.content.Should().BeEquivalentTo(content);
        result.comments.Should().ContainSingle().Which.Should().BeEquivalentTo(comment);
    }
}
