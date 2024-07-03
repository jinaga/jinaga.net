using System.Linq;

namespace Jinaga.Test.Specifications;

public class DistributionRuleTest
{
    [Fact]
    public void DistributionRules_CanDescribe()
    {
        var description = DistributionRules.Describe(Distribution);
        description.Should().Be(
            """
            distribution {
                share (site: Blog.Site) {
                    content: Blog.Content [
                        content->site: Blog.Site = site
                        E {
                            publish: Blog.Content.Publish [
                                publish->content: Blog.Content = content
                            ]
                        }
                    ]
                } => content
                with everyone
                share (site: Blog.Site) {
                    content: Blog.Content [
                        content->site: Blog.Site = site
                    ]
                } => content
                with (site: Blog.Site) {
                    creator: Jinaga.User [
                        creator = site->creator: Jinaga.User
                    ]
                } => creator
                share (site: Blog.Site) {
                    content: Blog.Content [
                        content->site: Blog.Site = site
                    ]
                    comment: Blog.Comment [
                        comment->content: Blog.Content = content
                    ]
                } => comment
                with (site: Blog.Site) {
                    creator: Jinaga.User [
                        creator = site->creator: Jinaga.User
                    ]
                } => creator
                share (site: Blog.Site, author: Jinaga.User) {
                    content: Blog.Content [
                        content->site: Blog.Site = site
                        E {
                            publish: Blog.Content.Publish [
                                publish->content: Blog.Content = content
                            ]
                        }
                    ]
                    comment: Blog.Comment [
                        comment->content: Blog.Content = content
                        comment->author: Jinaga.User = author
                    ]
                } => comment
                with (site: Blog.Site, author: Jinaga.User) {
                } => author
            }
            """.Replace("\r\n", "\n"));
    }

    private DistributionRules Distribution(DistributionRules r) => r
        // Everyone can see published posts
        .Share(Given<Model.Site>.Match((site, facts) =>
            from content in facts.OfType<Model.Content>()
            where content.site == site &&
                facts.Any<Model.Publish>(publish => publish.content == content)
            select content
        ))
        .WithEveryone()
        // The creator can see all posts
        .Share(Given<Model.Site>.Match((site, facts) =>
            from content in facts.OfType<Model.Content>()
            where content.site == site
            select content
        ))
        .With(site => site.creator)
        // The creator can see all comments
        .Share(Given<Model.Site>.Match((site, facts) =>
        from content in facts.OfType<Model.Content>()
            where content.site == site
            from comment in facts.OfType<Model.Comment>()
            where comment.content == content
            select comment
        ))
        .With(site => site.creator)
        // A comment author can see their own comments on published posts
        .Share(Given<Model.Site, User>.Match((site, author, facts) =>
            from content in facts.OfType<Model.Content>()
            where content.site == site &&
                facts.Any<Model.Publish>(publish => publish.content == content)
            from comment in facts.OfType<Model.Comment>()
            where comment.content == content &&
                comment.author == author
            select comment
        ))
        .With(Given<Model.Site, User>.Match((site, author) =>
            author
        ))
        ;
}
