using FluentAssertions;
using Xunit;
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
                share (p1: Blog) {
                    u1: Post [
                        u1->blog: Blog = p1
                    ]
                } => u1
                with (p1: Blog) {
                    u1: Jinaga.User [
                        u1 = p1->creator: Jinaga.User
                    ]
                } => u1
                share (p1: Blog) {
                    u1: Post [
                        u1->blog: Blog = p1
                    ]
                    u2: Comment [
                        u2->post: Post = u1
                    ]
                } => u2
                with (p1: Blog) {
                    u1: Jinaga.User [
                        u1 = p1->creator: Jinaga.User
                    ]
                } => u1
                share (p1: Blog, p2: Jinaga.User) {
                    u1: Post [
                        u1->blog: Blog = p1
                        E {
                            u2: Publish [
                                u2->post: Post = u1
                            ]
                        }
                    ]
                    u3: Comment [
                        u3->post: Post = u1
                        u3->author: Jinaga.User = p2
                    ]
                } => u3
                with (p1: Blog, p2: Jinaga.User) {
                    u1: Jinaga.User [
                        u1 = p2
                    ]
                } => u1
            }
            """.Replace("\r\n", "\n"));
    }

    /*
  // Everyone can see published posts
  .share(model.given(Blog).match((blog, facts) =>
    facts.ofType(Post)
      .join(post => post.blog, blog)
      .exists(post => facts.ofType(Publish)
        .join(publish => publish.post, post)
      )
  )).withEveryone()
  // The creator can see all posts
  .share(model.given(Blog).match((blog, facts) =>
    facts.ofType(Post)
      .join(post => post.blog, blog)
  )).with(model.given(Blog).match((blog, facts) =>
      facts.ofType(User)
        .join(user => user, blog.creator)
  ))
  // The creator can see all comments
  .share(model.given(Blog).match((blog, facts) =>
    facts.ofType(Post)
      .join(post => post.blog, blog)
      .selectMany(post => facts.ofType(Comment)
        .join(comment => comment.post, post)
      )
  )).with(model.given(Blog).match((blog, facts) =>
    facts.ofType(User)
      .join(user => user, blog.creator)
  ))
  // A comment author can see their own comments on published posts
  .share(model.given(Blog, User).match((blog, author, facts) =>
    facts.ofType(Post)
      .join(post => post.blog, blog)
      .exists(post => facts.ofType(Publish)
        .join(publish => publish.post, post)
      )
      .selectMany(post => facts.ofType(Comment)
        .join(comment => comment.post, post)
        .join(comment => comment.author, author)
      )
  )).with(model.given(Blog, User).match((blog, author, facts) =>
    facts.ofType(User)
      .join(user => user, author)
  ))
     */

    private DistributionRules Distribution(DistributionRules r) => r
        .Share(Given<Model.Site>.Match((site, facts) =>
            from content in facts.OfType<Model.Content>()
            where content.site == site &&
                facts.Any<Model.Publish>(publish => publish.content == content)
            select content
        )).WithEveryone()
        ;
}
