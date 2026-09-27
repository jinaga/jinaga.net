using System.Collections.Immutable;
using System.Linq;
using Jinaga.Pipelines;
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

        var (head, tail) = Split(specification);

        head.Should().BeSameAs(specification);
        tail.Should().BeNull();
    }

    [Fact]
    public void PutsAllInHeadIfOnlyPredecessorJoins()
    {
        Specification specification = Given<Model.Content>.Match(content => content.site.creator);

        var (head, tail) = Split(specification);

        head.Should().BeSameAs(specification);
        tail.Should().BeNull();
    }

    [Fact]
    public void RunsTheWholeSpecificationInTheTailIfOnlySuccessorJoins()
    {
        Specification specification = Given<Model.Site>.Match((site, facts) =>
            facts.OfType<Model.Content>(content => content.site == site));

        var (head, tail) = Split(specification);

        // The head has no matches. It projects the given, which the tail needs.
        Describe(head).Should().Be(
            """
            (site: Blog.Site) {
            } => {
                site = site
            }

            """);
        Describe(tail).Should().Be(Describe(specification));
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

        var (head, tail) = Split(specification);

        Describe(head).Should().Be(
            """
            (content: Blog.Content) {
                site: Blog.Site [
                    site = content->site: Blog.Site
                ]
            } => {
                site = site
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

        var (head, tail) = Split(specification);

        Describe(head).Should().Be(
            """
            (content: Blog.Content) {
                __s0: Blog.Site [
                    __s0 = content->site: Blog.Site
                ]
            } => {
                __s0 = __s0
            }

            """);
        Describe(tail).Should().Be(
            """
            (__s0: Blog.Site) {
                guest: Blog.GuestBlogger [
                    guest->site: Blog.Site = __s0
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

        var (head, tail) = Split(specification);

        Describe(head).Should().Be(
            """
            (content: Blog.Content) {
                __s0: Blog.Site [
                    __s0 = content->site: Blog.Site
                ]
            } => {
                __s0 = __s0
            }

            """);
        Describe(tail).Should().Be(
            """
            (__s0: Blog.Site) {
                guest: Blog.GuestBlogger [
                    guest->site: Blog.Site = __s0
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

        var (head, tail) = Split(specification);

        Describe(head).Should().Be(
            """
            (content: Blog.Content) {
                site: Blog.Site [
                    site = content->site: Blog.Site
                ]
            } => {
                site = site
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
    public void TheSplitLabelIsReservedSoItCannotCollideWithADeclaredLabel()
    {
        // The given is named as the old split would have named its label. The split's labels
        // begin with "__", which no well-formed specification declares.
        Specification specification = Given<Model.Content>.Match((s1, facts) =>
            facts.OfType<Model.GuestBlogger>(guest => guest.site == s1.site));

        var (head, tail) = Split(specification);

        Describe(head).Should().Be(
            """
            (s1: Blog.Content) {
                __s0: Blog.Site [
                    __s0 = s1->site: Blog.Site
                ]
            } => {
                __s0 = __s0
            }

            """);
        Describe(tail).Should().Be(
            """
            (__s0: Blog.Site) {
                guest: Blog.GuestBlogger [
                    guest->site: Blog.Site = __s0
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

        var (head, tail) = Split(specification);

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

        var (head, tail) = Split(specification);

        Describe(head).Should().Be(
            """
            (content: Blog.Content) {
                creator: Jinaga.User [
                    creator = content->site: Blog.Site->creator: Jinaga.User
                ]
                __s0: Blog.Site [
                    __s0 = content->site: Blog.Site
                ]
            } => {
                __s0 = __s0
                creator = creator
            }

            """);
        Describe(tail).Should().Be(
            """
            (creator: Jinaga.User, __s0: Blog.Site) {
                guest: Blog.GuestBlogger [
                    guest->site: Blog.Site = __s0
                ]
            } => creator

            """);
    }

    [Fact]
    public void DoesNotHoistAWalkFromBeneathTwoNestedNegativeExistentialConditions()
    {
        // Two negations do not cancel. Beneath the inner one, a walk from p1 moved into the head
        // would be tried one reached fact at a time, and the outer negation would then ask, for
        // each archive, whether some fact restores it, where the specification asks whether one
        // fact restores them all. So the walk stays in the tail, which is given p1.
        //
        //   (p1: Link) {
        //       u1: Owner [
        //           u1->workspace: Workspace = p1->item: Item->workspace: Workspace
        //           !E {
        //               u2: Archive [
        //                   u2->workspace: Workspace = u1->workspace: Workspace
        //                   !E {
        //                       u3: Restore [
        //                           u3->archive: Archive = u2
        //                           u3->workspace: Workspace = p1->parent: Item->workspace: Workspace
        //                       ]
        //                   }
        //               ]
        //           }
        //       ]
        //   } => u1
        var workspace = new Role("workspace", "Workspace");
        var restore = new Match(
            new Label("u3", "Restore"),
            ImmutableList.Create(
                new PathCondition(ImmutableList.Create(new Role("archive", "Archive")), "u2", ImmutableList<Role>.Empty),
                new PathCondition(ImmutableList.Create(workspace), "p1",
                    ImmutableList.Create(new Role("parent", "Item"), workspace))),
            ImmutableList<ExistentialCondition>.Empty);
        var archive = new Match(
            new Label("u2", "Archive"),
            ImmutableList.Create(new PathCondition(ImmutableList.Create(workspace), "u1", ImmutableList.Create(workspace))),
            ImmutableList.Create(new ExistentialCondition(false, ImmutableList.Create(restore))));
        var owner = new Match(
            new Label("u1", "Owner"),
            ImmutableList.Create(new PathCondition(ImmutableList.Create(workspace), "p1",
                ImmutableList.Create(new Role("item", "Item"), workspace))),
            ImmutableList.Create(new ExistentialCondition(false, ImmutableList.Create(archive))));
        var specification = new Specification(
            ImmutableList.Create(new SpecificationGiven(new Label("p1", "Link"), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(owner),
            new SimpleProjection("u1", typeof(object)));

        var (_, tail) = Split(specification);

        tail!.Givens.Select(given => given.Label).Should().Equal(
            new Label("p1", "Link"),
            new Label("__s0", "Workspace"));
    }

    private static (Specification head, Specification? tail) Split(Specification specification) =>
        WellFormedSpecification.Check(specification, "The specification").SplitBeforeFirstSuccessor();

    private static string Describe(Specification specification)
    {
        specification.Should().NotBeNull();
        return specification.ToDescriptiveString().ReplaceLineEndings("\n");
    }
}
