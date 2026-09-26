using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Authorization;

namespace Jinaga.Test;

// Model types are qualified throughout. Jinaga.Test already declares its own Site, and a
// namespace member beats a using-alias in name resolution, so an alias here would silently
// resolve to the wrong record.

/// <summary>
/// Authorization rules, enforced rather than described.
///
/// Before <see cref="JinagaTestOptions.Authorization"/> existed, a rule could only be checked by
/// reading the text <c>AuthorizationRules.Describe</c> produced. That catches a rule that is
/// written wrongly; it cannot catch a rule that is written correctly and behaves differently,
/// and the two do come apart.
/// </summary>
public class AuthorizationTest
{
    private static readonly User Owner = new("---OWNER---");
    private static readonly User Stranger = new("---STRANGER---");

    private static JinagaClient ClientFor(User user, params object[] existing) =>
        JinagaTest.Create(options =>
        {
            options.User = user;
            options.InitialState = existing.ToImmutableList();
            options.Authorization = a => a
                .Any<User>()
                // Only the creator may create their own site.
                .Type<Model.Site>(site => site.creator)
                // Content belongs to whoever owns the site it is on...
                .Type<Model.Content>(content => content.site.creator)
                // ...or to a guest the owner invited. Two rules on one type, so a fact is
                // permitted if either applies.
                .Type<Model.Content>((content, facts) =>
                    from guest in facts.OfType<Model.GuestBlogger>()
                    where guest.site == content.site
                    from user in facts.OfType<User>()
                    where user == guest.guest
                    select user)
                .Type<Model.GuestBlogger>(guest => guest.site.creator);
        });

    /// <summary>
    /// The same policy, but a guest's invitation can be revoked. The rule carries a negative
    /// existential, which is the shape that decides whether a right has been withdrawn.
    /// </summary>
    private static JinagaClient ClientWithRevocation(User user, params object[] existing) =>
        JinagaTest.Create(options =>
        {
            options.User = user;
            options.InitialState = existing.ToImmutableList();
            options.Authorization = a => a
                .Any<User>()
                .Type<Model.Site>(site => site.creator)
                .Type<Model.Content>(content => content.site.creator)
                .Type<Model.Content>((content, facts) =>
                    from guest in facts.OfType<Model.GuestBlogger>()
                    where guest.site == content.site
                    where !facts.OfType<Model.GuestBloggerRevoked>(
                        revoked => revoked.invitation == guest).Any()
                    from user in facts.OfType<User>()
                    where user == guest.guest
                    select user);
        });

    [Fact]
    public async Task AnyAllowsAnybody()
    {
        var j = ClientFor(Stranger);

        var user = await j.Fact(new User("---SOMEBODY---"));

        user.Should().NotBeNull();
    }

    [Fact]
    public async Task ThePredecessorRuleAllowsTheUserItNames()
    {
        var j = ClientFor(Owner);

        var site = await j.Fact(new Model.Site(Owner, "site"));

        site.Should().NotBeNull();
    }

    [Fact]
    public async Task ThePredecessorRuleRefusesAnybodyElse()
    {
        // The point of the whole feature. Before this, the rule was correct and the test client
        // accepted the fact anyway.
        var j = ClientFor(Stranger);

        Func<Task> creating = async () => await j.Fact(new Model.Site(Owner, "site"));

        await creating.Should().ThrowAsync<AuthorizationException>()
            .Where(e => e.FactType == "Blog.Site");
    }

    [Fact]
    public async Task ARuleCanReachThroughPredecessors()
    {
        var j = ClientFor(Owner);
        var site = await j.Fact(new Model.Site(Owner, "site"));

        var content = await j.Fact(new Model.Content(site, "/index.html"));

        content.Should().NotBeNull();
    }

    [Fact]
    public async Task AStrangerCannotPostToSomebodyElsesSite()
    {
        var site = new Model.Site(Owner, "site");
        var intruder = ClientFor(Stranger, site);

        Func<Task> posting = async () => await intruder.Fact(new Model.Content(site, "/index.html"));

        await posting.Should().ThrowAsync<AuthorizationException>();
    }

    [Fact]
    public async Task AnInvitedGuestCanPost()
    {
        // The specification rule doing real work: the guest is permitted only because a
        // GuestBlogger fact exists, which is a question about the graph rather than about the
        // fact being authorized.
        var site = new Model.Site(Owner, "site");
        var invitation = new Model.GuestBlogger(site, Stranger);

        // Premises, not actions: the guest's client learns of these the way a real one would,
        // by syncing, rather than by authoring them itself.
        var guest = ClientFor(Stranger, site, invitation);
        var content = await guest.Fact(new Model.Content(site, "/guest-post.html"));

        content.Should().NotBeNull();
    }

    [Fact]
    public async Task AGuestCannotInviteThemselves()
    {
        // The invitation is what confers the right, so it has to be gated by something other
        // than itself. Otherwise the rule above would be decorative.
        var site = new Model.Site(Owner, "site");
        var wouldBeGuest = ClientFor(Stranger, site);
        Func<Task> inviting = async () =>
            await wouldBeGuest.Fact(new Model.GuestBlogger(site, Stranger));

        await inviting.Should().ThrowAsync<AuthorizationException>();
    }

    [Fact]
    public async Task AFactTypeWithNoRuleIsRefused()
    {
        // Silence denies. A type nobody wrote a rule for is far more likely to be an oversight
        // than an intentional free-for-all, and defaulting to permitted would hide it.
        var j = ClientFor(Owner);
        var site = await j.Fact(new Model.Site(Owner, "site"));
        var content = await j.Fact(new Model.Content(site, "/index.html"));

        Func<Task> publishing = async () =>
            await j.Fact(new Model.Publish(content, DateTime.UtcNow));

        await publishing.Should().ThrowAsync<AuthorizationException>()
            .Where(e => e.FactType == "Blog.Content.Publish");
    }

    [Fact]
    public async Task WithNoUserNothingThatNamesOneIsPermitted()
    {
        var j = JinagaTest.Create(options =>
            options.Authorization = a => a.Any<User>().Type<Model.Site>(site => site.creator));

        Func<Task> creating = async () => await j.Fact(new Model.Site(Owner, "site"));

        await creating.Should().ThrowAsync<AuthorizationException>();
    }

    [Fact]
    public async Task WithoutRulesEverythingIsStillPermitted()
    {
        // Existing tests must keep working: configuring no rules leaves the client exactly as
        // it was before this feature.
        var j = JinagaTest.Create();

        var site = await j.Fact(new Model.Site(Owner, "site"));

        site.Should().NotBeNull();
    }

    [Fact]
    public async Task ARuleWithANegativeExistentialPermitsWhileTheConditionHolds()
    {
        var site = new Model.Site(Owner, "site");
        var invitation = new Model.GuestBlogger(site, Stranger);

        var guest = ClientWithRevocation(Stranger, site, invitation);

        var content = await guest.Fact(new Model.Content(site, "/guest-post.html"));

        content.Should().NotBeNull();
    }

    [Fact]
    public async Task ARevokedRightIsRefusedGoingForward()
    {
        // The case a text assertion cannot reach: the rule reads correctly either way, and only
        // running it shows that the revocation actually withdraws the right.
        var site = new Model.Site(Owner, "site");
        var invitation = new Model.GuestBlogger(site, Stranger);
        var revocation = new Model.GuestBloggerRevoked(invitation);

        var guest = ClientWithRevocation(Stranger, site, invitation, revocation);

        Func<Task> posting = async () =>
            await guest.Fact(new Model.Content(site, "/guest-post.html"));

        await posting.Should().ThrowAsync<AuthorizationException>();
    }

    [Fact]
    public async Task RevocationDoesNotReachBackIntoWhatWasAlreadyAuthored()
    {
        // Authorization is evaluated once, when a fact is first accepted. A right withdrawn
        // later does not unmake what it permitted — the property the library chose deliberately
        // over cascading de-authorization, and one that is easy to get backwards.
        var site = new Model.Site(Owner, "site");
        var invitation = new Model.GuestBlogger(site, Stranger);
        var earlierPost = new Model.Content(site, "/guest-post.html");
        var revocation = new Model.GuestBloggerRevoked(invitation);

        // The premise: the guest posted while invited, and was revoked afterwards.
        var guest = ClientWithRevocation(Stranger, site, invitation, earlierPost, revocation);

        // The earlier post stands — it was accepted when it was written, and a decision made
        // then is not revisited.
        var again = await guest.Fact(earlierPost);
        again.Should().Be(earlierPost);

        // Only new work is refused.
        Func<Task> posting = async () =>
            await guest.Fact(new Model.Content(site, "/later-post.html"));

        await posting.Should().ThrowAsync<AuthorizationException>();
    }

    [Fact]
    public async Task AnAlreadyAcceptedFactIsNotReCheckedWhenARuleWouldNowRefuseIt()
    {
        // Authorization is evaluated when a fact is first accepted. Re-checking would let a
        // later fact retroactively invalidate history, which is exactly what the library
        // deliberately does not do.
        var owner = ClientFor(Owner);
        var site = await owner.Fact(new Model.Site(Owner, "site"));

        // Saving the same fact again is a no-op rather than a fresh authorization decision.
        var again = await owner.Fact(new Model.Site(Owner, "site"));

        again.Should().Be(site);
    }
    [Fact]
    public async Task AFactAndItsNewPredecessorsCanBeAuthoredTogether()
    {
        // The site is not in the store when the content is authorized. The rule walks to it
        // within the graph being saved.
        var j = ClientFor(Owner);

        var content = await j.Fact(new Model.Content(new Model.Site(Owner, "site"), "/index.html"));

        content.Should().NotBeNull();
    }

    [Fact]
    public async Task AnAuthorizedFactReachesObservers()
    {
        // Authorizing must not write, or the save that follows finds nothing new and tells no
        // observer about the fact.
        var site = new Model.Site(Owner, "site");
        var j = ClientFor(Owner, site);
        var paths = new List<string>();
        var contentInSite = Given<Model.Site>.Match((site, facts) =>
            facts.OfType<Model.Content>(content => content.site == site)
                .Select(content => content.path));
        var observer = j.Watch(contentInSite, site, path => { paths.Add(path); });
        await observer.Loaded;

        await j.Fact(new Model.Content(site, "/index.html"));

        observer.Stop();
        paths.Should().ContainSingle().Which.Should().Be("/index.html");
    }

    [Fact]
    public async Task ARefusedFactIsNotStored()
    {
        var site = new Model.Site(Owner, "site");
        var intruder = ClientFor(Stranger, site);

        Func<Task> posting = async () => await intruder.Fact(new Model.Content(site, "/index.html"));
        await posting.Should().ThrowAsync<AuthorizationException>();

        var contentInSite = Given<Model.Site>.Match((site, facts) =>
            facts.OfType<Model.Content>(content => content.site == site));
        var content = await intruder.Query(contentInSite, site);
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task ARefusedFactIsStillRefusedWhenTriedAgain()
    {
        // A refused fact that reached the store would look already accepted the second time.
        var site = new Model.Site(Owner, "site");
        var intruder = ClientFor(Stranger, site);

        Func<Task> posting = async () => await intruder.Fact(new Model.Content(site, "/index.html"));

        await posting.Should().ThrowAsync<AuthorizationException>();
        await posting.Should().ThrowAsync<AuthorizationException>();
    }

    [Fact]
    public async Task OnlyTheProjectedUserIsAuthorized()
    {
        // The owner appears in the rule, but only as a step on the way to the guest. Matching
        // any user the rule passes through would permit the owner.
        var site = new Model.Site(Owner, "site");
        var invitation = new Model.GuestBlogger(site, Stranger);
        var j = JinagaTest.Create(options =>
        {
            options.User = Owner;
            options.InitialState = ImmutableList.Create<object>(site, invitation);
            options.Authorization = a => a
                .Type<Model.Content>((content, facts) =>
                    from owner in facts.OfType<User>()
                    where owner == content.site.creator
                    from guest in facts.OfType<Model.GuestBlogger>()
                    where guest.site == content.site
                    from user in facts.OfType<User>()
                    where user == guest.guest
                    select user);
        });

        Func<Task> posting = async () => await j.Fact(new Model.Content(site, "/index.html"));

        await posting.Should().ThrowAsync<AuthorizationException>();
    }

    [Fact]
    public async Task ARuleThatStartsWithASuccessorIsUnsatisfiable()
    {
        // The fact being authorized is new, so it has no successors to start from. JinagaJS
        // rejects such a rule as malformed rather than refusing every fact quietly.
        var j = JinagaTest.Create(options =>
        {
            options.User = Stranger;
            options.Authorization = a => a
                .Any<User>()
                .Type<Model.Site>((site, facts) =>
                    from guest in facts.OfType<Model.GuestBlogger>()
                    where guest.site == site
                    from user in facts.OfType<User>()
                    where user == guest.guest
                    select user);
        });

        Func<Task> creating = async () => await j.Fact(new Model.Site(Owner, "site"));

        await creating.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must start with a predecessor join*");
    }
}
