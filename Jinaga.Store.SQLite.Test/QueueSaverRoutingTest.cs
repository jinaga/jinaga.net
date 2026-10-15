using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Store.SQLite.Test.Fakes;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test;

/// <summary>
/// The same queue routing as in Jinaga.Test, against the store that a mobile
/// application actually uses. The record of who saved each fact lives in the
/// database here, so it has to survive the application being closed and opened again: a user
/// who creates facts offline and does not come back until tomorrow is the
/// ordinary case, not the edge case.
/// </summary>
public class QueueSaverRoutingTest
{
    /// <summary>
    /// Long enough that the debounce never elapses during a test, so the only
    /// thing that sends the queue is an explicit Push.
    /// </summary>
    private const int DelayLongerThanAnyTest = 60000;

    private static readonly User UserA = new User("---FAKE PUBLIC KEY A---");
    private static readonly User UserB = new User("---FAKE PUBLIC KEY B---");

    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "QueueSaverRoutingTest.db");

    private readonly ITestOutputHelper output;
    private readonly FakeNetwork network;
    private readonly FakeIdentitySource identity;

    public QueueSaverRoutingTest(ITestOutputHelper output)
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        this.output = output;
        network = new FakeNetwork(output);
        identity = new FakeIdentitySource();
        network.CurrentUser = () => identity.CurrentUser;
    }

    [Fact]
    public async Task Push_AfterAnotherUserLogsIn_HoldsTheFirstUsersFact()
    {
        var store = GivenStore();
        var j = GivenClient(store);

        identity.SetCurrentUser(UserA);
        var siteA = await j.Fact(new Site("a.example.com"));
        identity.SetCurrentUser(UserB);
        var siteB = await j.Fact(new Site("b.example.com"));

        await j.Push();

        UploadedHashes().Should().Contain(j.Hash(siteB));
        UploadedHashes().Should().NotContain(j.Hash(siteA));
        network.UploadedUnderUsers.Should().Equal(UserB);
        (await QueuedHashes(store)).Should().Equal(j.Hash(siteA));

        await j.Unload();
    }

    [Fact]
    public async Task Push_WhenTheSaverLogsBackIn_SendsWhatWasHeldWithoutANewSave()
    {
        var store = GivenStore();
        var j = GivenClient(store);

        identity.SetCurrentUser(UserA);
        var siteA = await j.Fact(new Site("a.example.com"));
        identity.SetCurrentUser(UserB);
        await j.Fact(new Site("b.example.com"));
        await j.Push();

        // No fact is saved from here on. The only thing that changes is who is
        // logged in.
        identity.SetCurrentUser(UserA);
        await j.Push();

        UploadedHashes().Should().Contain(j.Hash(siteA));
        network.UploadedUnderUsers.Should().Equal(UserB, UserA);
        (await QueuedHashes(store)).Should().BeEmpty();

        await j.Unload();
    }

    [Fact]
    public async Task Push_WithAFactWhoseAncestorAnotherUserQueued_HoldsItUntilTheAncestorIsSent()
    {
        var store = GivenStore();
        var j = GivenClient(store);

        identity.SetCurrentUser(UserA);
        var site = await j.Fact(new Site("a.example.com"));
        identity.SetCurrentUser(UserB);
        var post = await j.Fact(new Post(site, "2026-10-13T00:00:00Z"));

        // The post is this user's, but its predecessor is not, and sending it
        // would carry the predecessor along under the wrong identity.
        await j.Push();
        UploadedHashes().Should().BeEmpty();
        (await QueuedHashes(store)).Should().Equal(j.Hash(site), j.Hash(post));

        identity.SetCurrentUser(UserA);
        await j.Push();
        UploadedHashes().Should().Equal(j.Hash(site));
        (await QueuedHashes(store)).Should().Equal(j.Hash(post));

        identity.SetCurrentUser(UserB);
        await j.Push();
        UploadedHashes().Should().Contain(j.Hash(post));
        (await QueuedHashes(store)).Should().BeEmpty();

        await j.Unload();
    }

    [Fact]
    public async Task Push_AfterTheStoreIsClosedAndOpenedAgain_StillHoldsTheFactForItsSaver()
    {
        string siteAHash;
        string siteBHash;

        var firstStore = GivenStore();
        var firstClient = GivenClient(firstStore);
        identity.SetCurrentUser(UserA);
        var siteA = await firstClient.Fact(new Site("a.example.com"));
        siteAHash = firstClient.Hash(siteA);
        identity.SetCurrentUser(UserB);
        var siteB = await firstClient.Fact(new Site("b.example.com"));
        siteBHash = firstClient.Hash(siteB);
        await firstClient.Unload();

        // A second store on the same database is what the application has after
        // it has been closed and opened again.
        var secondStore = GivenStore();
        var secondClient = GivenClient(secondStore);

        // User B is still the one logged in, so A's fact is still held.
        await secondClient.Push();
        UploadedHashes().Should().Contain(siteBHash);
        UploadedHashes().Should().NotContain(siteAHash);
        (await QueuedHashes(secondStore)).Should().Equal(siteAHash);

        identity.SetCurrentUser(UserA);
        await secondClient.Push();
        UploadedHashes().Should().Contain(siteAHash);
        (await QueuedHashes(secondStore)).Should().BeEmpty();

        await secondClient.Unload();
    }

    [Fact]
    public async Task Push_AfterAnotherUserSavesTheSameFact_SendsTheirDescendantsWithoutTheFirstUser()
    {
        var store = GivenStore();
        var j = GivenClient(store);

        identity.SetCurrentUser(UserA);
        var site = await j.Fact(new Site("a.example.com"));
        identity.SetCurrentUser(UserB);
        await j.Fact(site);
        var post = await j.Fact(new Post(site, "2026-10-13T00:00:00Z"));

        await j.Push();

        UploadedHashes().Should().Contain(j.Hash(site));
        UploadedHashes().Should().Contain(j.Hash(post));
        network.UploadedUnderUsers.Should().Equal(UserB);
        (await QueuedHashes(store)).Should().BeEmpty();

        await j.Unload();
    }

    [Fact]
    public async Task LoadSavers_AfterTwoUsersSaveTheSameFact_ReportsBothAfterTheStoreIsOpenedAgain()
    {
        var firstStore = GivenStore();
        var firstClient = GivenClient(firstStore);
        identity.SetCurrentUser(UserA);
        var site = await firstClient.Fact(new Site("a.example.com"));
        identity.SetCurrentUser(UserB);
        await firstClient.Fact(site);
        var reference = new FactReference("Blog.Site", firstClient.Hash(site));
        await firstClient.Unload();

        IStore secondStore = GivenStore();
        var savers = await secondStore.LoadSavers(ImmutableList.Create(reference), CancellationToken.None);

        savers.Should().ContainKey(reference);
        savers[reference].Should().BeEquivalentTo(new[] { UserA.publicKey, UserB.publicKey });
    }

    [Fact]
    public async Task Push_WithRulesThatAdmitAnyUser_SendsAMessageToAUserAnotherUserIntroduced()
    {
        var store = GivenStore();
        var j = GivenClient(store, RoutingRules);
        var x = new User("---FAKE PUBLIC KEY X---");

        identity.SetCurrentUser(UserA);
        var project = await j.Fact(new RoutingProject(UserA, "Apollo"));
        var membership = await j.Fact(new RoutingMembership(project, x));
        identity.SetCurrentUser(null);

        identity.SetCurrentUser(UserB);
        var message = await j.Fact(new RoutingMessage(x, UserB, "Welcome"));

        await j.Push();

        UploadedHashes().Should().Contain(j.Hash(message));
        UploadedHashes().Should().Contain(j.Hash(x));
        UploadedHashes().Should().NotContain(j.Hash(project));
        UploadedHashes().Should().NotContain(j.Hash(membership));
        (await QueuedHashes(store)).Should().Equal(j.Hash(project), j.Hash(membership));

        await j.Unload();
    }

    [Fact]
    public async Task Push_WithoutRules_HoldsAMessageToAUserAnotherUserIntroduced()
    {
        var store = GivenStore();
        var j = GivenClient(store);
        var x = new User("---FAKE PUBLIC KEY X---");

        identity.SetCurrentUser(UserA);
        var project = await j.Fact(new RoutingProject(UserA, "Apollo"));
        await j.Fact(new RoutingMembership(project, x));
        identity.SetCurrentUser(null);

        identity.SetCurrentUser(UserB);
        var message = await j.Fact(new RoutingMessage(x, UserB, "Welcome"));

        await j.Push();

        UploadedHashes().Should().NotContain(j.Hash(message));
        UploadedHashes().Should().NotContain(j.Hash(x));
        (await QueuedHashes(store)).Should().Contain(j.Hash(message));

        await j.Unload();
    }

    [Fact]
    public async Task Push_WithNoProvider_SendsEveryQueuedFact()
    {
        var store = GivenStore();
        var options = new JinagaClientOptions
        {
            QueueProcessingDelay = DelayLongerThanAnyTest
        };
        var j = new JinagaClient(
            store,
            network,
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            options);

        var first = await j.Fact(new Site("a.example.com"));
        var second = await j.Fact(new Site("b.example.com"));

        await j.Push();

        UploadedHashes().Should().Contain(j.Hash(first));
        UploadedHashes().Should().Contain(j.Hash(second));
        (await QueuedHashes(store)).Should().BeEmpty();

        await j.Unload();
    }

    private static SQLiteStore GivenStore()
    {
        return new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
    }

    private static AuthorizationRules RoutingRules { get; } = AuthorizationRules.Build(rules => rules
        .Any<User>()
        .Type<RoutingProject>(project => project.creator)
        .Type<RoutingMembership>(membership => membership.project.creator)
        .Type<RoutingMessage>(message => message.from));

    private JinagaClient GivenClient(SQLiteStore store, AuthorizationRules? rules = null)
    {
        var options = new JinagaClientOptions
        {
            HttpAuthenticationProvider = identity,
            AuthorizationRules = rules,
            QueueProcessingDelay = DelayLongerThanAnyTest
        };
        return new JinagaClient(
            store,
            network,
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            options);
    }

    private IEnumerable<string> UploadedHashes()
    {
        return network.UploadedGraph.FactReferences
            .Select(reference => reference.Hash)
            .ToImmutableList();
    }

    /// <summary>
    /// What the queue still holds, whoever saved it, in the order it was
    /// queued.
    /// </summary>
    private static async Task<IEnumerable<string>> QueuedHashes(SQLiteStore store)
    {
        var queue = await store.GetQueue();
        return queue.Facts
            .Select(fact => fact.Reference.Hash)
            .ToImmutableList();
    }
}

[FactType("Test.Routing.Project")]
public record RoutingProject(User creator, string name);

[FactType("Test.Routing.Membership")]
public record RoutingMembership(RoutingProject project, User member);

[FactType("Test.Routing.Message")]
public record RoutingMessage(User to, User from, string text);
