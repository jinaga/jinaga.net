using System.Linq;
using Jinaga.Projections;

namespace Jinaga.Test.Specifications;

/// <summary>
/// Splitting a specification before its first successor join, as an authorization rule is
/// evaluated: the head runs on the graph being authorized, and the tail runs on the store.
/// Ported from JinagaJS test/specification/splitSpecificationSpec.ts.
/// </summary>
public class SplitSpecificationTest
{
    [Fact]
    public void PutsAllInHeadIfIdentitySpecification()
    {
        Specification specification = Given<Model.Site>.Match(site => site);

        var (head, tail) = specification.SplitBeforeFirstSuccessor();

        head.Should().BeSameAs(specification);
        tail.Should().BeNull();
    }

    [Fact]
    public void PutsAllInHeadIfOnlyPredecessorJoins()
    {
        Specification specification = Given<Model.Content>.Match(content => content.site.creator);

        var (head, tail) = specification.SplitBeforeFirstSuccessor();

        head.Should().BeSameAs(specification);
        tail.Should().BeNull();
    }

    [Fact]
    public void PutsAllInTailIfOnlySuccessorJoins()
    {
        Specification specification = Given<Model.Site>.Match((site, facts) =>
            facts.OfType<Model.Content>(content => content.site == site));

        var (head, tail) = specification.SplitBeforeFirstSuccessor();

        head.Should().BeNull();
        tail.Should().BeSameAs(specification);
    }

    [Fact]
    public void SplitsIfPredecessorAndThenSuccessor()
    {
        Specification specification = Given<Model.Content>.Match((content, facts) =>
            from site in facts.OfType<Model.Site>()
            where site == content.site
            from guest in facts.OfType<Model.GuestBlogger>()
            where guest.site == site
            select guest);

        var (head, tail) = specification.SplitBeforeFirstSuccessor();

        Describe(head).Should().Be(
            """
            (content: Blog.Content) {
                site: Blog.Site [
                    site = content->site: Blog.Site
                ]
            }

            """);
        Describe(tail).Should().Be(
            """
            (site: Blog.Site) {
                guest: Blog.GuestBlogger [
                    guest->site: Blog.Site = site
                ]
            } => guest

            """);
    }

    [Fact]
    public void SplitsIfPredecessorAndThenSuccessorInOneMatch()
    {
        Specification specification = Given<Model.Content>.Match((content, facts) =>
            facts.OfType<Model.GuestBlogger>(guest => guest.site == content.site));

        var (head, tail) = specification.SplitBeforeFirstSuccessor();

        Describe(head).Should().Be(
            """
            (content: Blog.Content) {
                s1: Blog.Site [
                    s1 = content->site: Blog.Site
                ]
            }

            """);
        Describe(tail).Should().Be(
            """
            (s1: Blog.Site) {
                guest: Blog.GuestBlogger [
                    guest->site: Blog.Site = s1
                ]
            } => guest

            """);
    }

    [Fact]
    public void SplitsPathWhenExistentialConditionExists()
    {
        Specification specification = Given<Model.Content>.Match((content, facts) =>
            from guest in facts.OfType<Model.GuestBlogger>()
            where guest.site == content.site
            where !facts.OfType<Model.GuestBloggerRevoked>(revoked => revoked.invitation == guest).Any()
            select guest);

        var (head, tail) = specification.SplitBeforeFirstSuccessor();

        Describe(head).Should().Be(
            """
            (content: Blog.Content) {
                s1: Blog.Site [
                    s1 = content->site: Blog.Site
                ]
            }

            """);
        Describe(tail).Should().Be(
            """
            (s1: Blog.Site) {
                guest: Blog.GuestBlogger [
                    guest->site: Blog.Site = s1
                    !E {
                        revoked: Blog.GuestBlogger.Revoked [
                            revoked->invitation: Blog.GuestBlogger = guest
                        ]
                    }
                ]
            } => guest

            """);
    }

    [Fact]
    public void SplitsWhenExistentialAppearsWithOnlySuccessorJoins()
    {
        Specification specification = Given<Model.Content>.Match((content, facts) =>
            from site in facts.OfType<Model.Site>()
            where site == content.site
            from guest in facts.OfType<Model.GuestBlogger>()
            where guest.site == site
            where !facts.OfType<Model.GuestBloggerRevoked>(revoked => revoked.invitation == guest).Any()
            select guest);

        var (head, tail) = specification.SplitBeforeFirstSuccessor();

        Describe(head).Should().Be(
            """
            (content: Blog.Content) {
                site: Blog.Site [
                    site = content->site: Blog.Site
                ]
            }

            """);
        Describe(tail).Should().Be(
            """
            (site: Blog.Site) {
                guest: Blog.GuestBlogger [
                    guest->site: Blog.Site = site
                    !E {
                        revoked: Blog.GuestBlogger.Revoked [
                            revoked->invitation: Blog.GuestBlogger = guest
                        ]
                    }
                ]
            } => guest

            """);
    }

    [Fact]
    public void TheSplitLabelDoesNotCollideWithAnExistingLabel()
    {
        Specification specification = Given<Model.Content>.Match((s1, facts) =>
            facts.OfType<Model.GuestBlogger>(guest => guest.site == s1.site));

        var (head, tail) = specification.SplitBeforeFirstSuccessor();

        Describe(head).Should().Be(
            """
            (s1: Blog.Content) {
                s2: Blog.Site [
                    s2 = s1->site: Blog.Site
                ]
            }

            """);
        Describe(tail).Should().Be(
            """
            (s2: Blog.Site) {
                guest: Blog.GuestBlogger [
                    guest->site: Blog.Site = s2
                ]
            } => guest

            """);
    }

    [Fact]
    public void KeepsSeveralPredecessorConditionsOnOneMatchInHead()
    {
        // Two predecessor paths that must reach the same fact. Both run on the graph.
        Specification specification = Given<Model.Comment>.Match((comment, facts) =>
            from user in facts.OfType<User>()
            where user == comment.author
            where user == comment.content.site.creator
            select user);

        var (head, tail) = specification.SplitBeforeFirstSuccessor();

        head.Should().BeSameAs(specification);
        tail.Should().BeNull();
    }

    [Fact]
    public void CarriesTheProjectedLabelIntoTheTail()
    {
        // The projection names a label the head binds, so the tail must be given it even
        // though none of its matches mention it.
        Specification specification = Given<Model.Content>.Match((content, facts) =>
            from creator in facts.OfType<User>()
            where creator == content.site.creator
            from guest in facts.OfType<Model.GuestBlogger>()
            where guest.site == content.site
            select creator);

        var (head, tail) = specification.SplitBeforeFirstSuccessor();

        Describe(head).Should().Be(
            """
            (content: Blog.Content) {
                creator: Jinaga.User [
                    creator = content->site: Blog.Site->creator: Jinaga.User
                ]
                s1: Blog.Site [
                    s1 = content->site: Blog.Site
                ]
            }

            """);
        Describe(tail).Should().Be(
            """
            (creator: Jinaga.User, s1: Blog.Site) {
                guest: Blog.GuestBlogger [
                    guest->site: Blog.Site = s1
                ]
            } => creator

            """);
    }

    private static string Describe(Specification specification)
    {
        specification.Should().NotBeNull();
        return specification.ToDescriptiveString().ReplaceLineEndings("\n");
    }
}
