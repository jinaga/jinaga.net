using Xunit;
using System.Linq;
using FluentAssertions;

namespace Jinaga.Test.Specifications;

public class AuthorizationRuleTest
{
    [Fact]
    public void AuthorizationRules_CanDescribe()
    {
        var description = AuthorizationRules.Describe(Authorization);
        description.Should().Be(
        """
        authorization {
            any Jinaga.User
            (site: Blog.Site) {
                user: Jinaga.User [
                    user = site->creator: Jinaga.User
                ]
            } => user
            (guestBlogger: Blog.GuestBlogger) {
                user: Jinaga.User [
                    user = guestBlogger->site: Blog.Site->creator: Jinaga.User
                ]
            } => user
            (content: Blog.Content) {
                user: Jinaga.User [
                    user = content->site: Blog.Site->creator: Jinaga.User
                ]
            } => user
            (content: Blog.Content) {
                guestBlogger: Blog.GuestBlogger [
                    guestBlogger->site: Blog.Site = content->site: Blog.Site
                ]
                user: Jinaga.User [
                    user = guestBlogger->guest: Jinaga.User
                ]
            } => user
            (comment: Blog.Comment) {
                user: Jinaga.User [
                    user = comment->author: Jinaga.User
                ]
            } => user
        }
        """);
    }

    private AuthorizationRules Authorization(AuthorizationRules r)
    {
        return r
            .Any<User>()
            .Type<Model.Site>(site => site.creator)
            .Type<Model.GuestBlogger>(guestBlogger => guestBlogger.site.creator)
            .Type<Model.Content>(content => content.site.creator)
            .Type<Model.Content>((content, facts) =>
                from guestBlogger in facts.OfType<Model.GuestBlogger>()
                where guestBlogger.site == content.site
                from user in facts.OfType<User>()
                where user == guestBlogger.guest
                select user
            )
            .Type<Model.Comment>(comment => comment.author)
            ;
    }
}
