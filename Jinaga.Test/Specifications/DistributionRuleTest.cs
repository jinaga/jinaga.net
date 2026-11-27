using System.Collections.Immutable;
using System.Linq;
using Jinaga.Pipelines;
using Jinaga.Projections;

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

    /// <summary>
    /// A rule the replicator could not load is refused where it is written.
    ///
    /// <para>
    /// The replicator decides whether to serve a feed by comparing its skeleton with the
    /// skeletons of the feeds of every rule, so a rule whose feeds cannot be built is one it
    /// cannot answer with. Accepting it here and failing later puts the failure arbitrarily
    /// far from the mistake: before this, the first sign was a 403 or an empty result on a
    /// deployed client.
    /// </para>
    /// </summary>
    [Fact]
    public void ShareRefusesASpecificationThatHasNoFeeds()
    {
        var rules = new DistributionRules(ImmutableList<DistributionRule>.Empty);

        var sharing = () => rules.Share(Unanchored);

        sharing.Should().Throw<SpecificationException>()
            .WithMessage("A replicator could not load this distribution rule*")
            // The reason the feeds could not be built, from FeedBuilder.
            .WithMessage("*has no path condition*")
            // And the rule itself, which is all that names it: Share is handed a
            // specification, and the user it is shared with is not chosen until With.
            .WithMessage("*The rule shares:*content: Blog.Content*");
    }

    /// <summary>
    /// The refusal happens at the <c>Share</c> call, not when the rules are later described.
    /// The flag is set only if <c>Share</c> returns, so a library that stored the rule and
    /// refused it at description time would leave it set.
    /// </summary>
    [Fact]
    public void ShareRefusesBeforeTheRulesAreDescribed()
    {
        var shareReturned = false;

        var describing = () => DistributionRules.Describe(r =>
        {
            var target = r.Share(Unanchored);
            shareReturned = true;
            return target.WithEveryone();
        });

        describing.Should().Throw<SpecificationException>();
        shareReturned.Should().BeFalse();
    }

    /// <summary>
    /// A rule whose feeds can be built is accepted, so the refusal above is the shape and not
    /// the check. <see cref="DistributionRules_CanDescribe"/> covers the same ground over
    /// every rule of <see cref="Distribution"/>; this states it as its own expectation.
    /// </summary>
    [Fact]
    public void ShareAcceptsASpecificationThatHasFeeds()
    {
        var rules = new DistributionRules(ImmutableList<DistributionRule>.Empty);

        var sharing = () => rules.Share(Given<Model.Site>.Match((site, facts) =>
            from content in facts.OfType<Model.Content>()
            where content.site == site
            select content
        ));

        sharing.Should().NotThrow();
    }

    /// <summary>
    /// A specification whose match no path condition anchors, so it would range over every
    /// <c>Blog.Content</c> fact in the store and has no feeds. The model builder cannot
    /// produce this shape -- it anchors every match by construction -- so it is built
    /// directly, as <see cref="FeedBuilderTest"/> builds its own refusals.
    /// </summary>
    private static Specification<Model.Site, Model.Content> Unanchored =>
        new(
            ImmutableList.Create(new SpecificationGiven(
                new Label("site", "Blog.Site"), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(new Match(
                new Label("content", "Blog.Content"),
                ImmutableList<PathCondition>.Empty,
                ImmutableList<ExistentialCondition>.Empty)),
            new SimpleProjection("content", typeof(Model.Content)));

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
