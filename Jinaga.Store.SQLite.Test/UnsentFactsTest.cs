using Jinaga.Projections;
using Jinaga.Store.SQLite.Test.Fakes;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test;

/// <summary>
/// The same counts as in Jinaga.Test, against the store a mobile application actually uses.
/// The record of who saved each fact lives in the database here, so the counts an
/// application shows before logging a user out have to come from it: a user who creates
/// facts offline and does not come back until tomorrow is the ordinary case.
///
/// Each count is taken from the same classification of the queue that decides what a save
/// sends, so every test here checks the counts against what the push actually did.
/// </summary>
public class UnsentFactsTest : SQLiteTest
{
    /// <summary>
    /// Long enough that the debounce never elapses during a test, so the only
    /// thing that sends the queue is an explicit Push.
    /// </summary>
    private const int DelayLongerThanAnyTest = 60000;

    private static readonly User UserA = new User("---FAKE PUBLIC KEY A---");
    private static readonly User UserB = new User("---FAKE PUBLIC KEY B---");

    private readonly ITestOutputHelper output;
    private readonly FakeNetwork network;
    private readonly FakeIdentitySource identity;

    public UnsentFactsTest(ITestOutputHelper output)
    {
        this.output = output;
        network = new FakeNetwork(output);
        identity = new FakeIdentitySource();
        network.CurrentUser = () => identity.CurrentUser;
    }

    [Fact]
    public async Task GetUnsentFacts_WithTheSaverStillLoggedIn_CountsTheirFactsAsTheirsAndHoldsNothing()
    {
        var j = GivenClient(GivenStore());

        identity.SetCurrentUser(UserA);
        await j.Fact(new Site("a.example.com"));
        await j.Fact(new Site("b.example.com"));

        var unsent = await j.GetUnsentFacts();

        unsent.Total.Should().Be(2);
        unsent.Held.Should().Be(0, "the user who saved them is the one logged in");
        unsent.SavedByCurrentUser.Should().Be(2, "these are the facts that become held if this user logs out");
    }

    [Fact]
    public async Task GetUnsentFacts_AfterAnotherUserLogsIn_CountsTheFirstUsersFactsAsHeld()
    {
        var store = GivenStore();
        var j = GivenClient(store);

        identity.SetCurrentUser(UserA);
        var siteA = await j.Fact(new Site("a.example.com"));
        var siteB = await j.Fact(new Site("b.example.com"));
        identity.SetCurrentUser(UserB);

        var unsent = await j.GetUnsentFacts();

        unsent.Total.Should().Be(2);
        unsent.Held.Should().Be(2);
        unsent.SavedByCurrentUser.Should().Be(0, "this user has saved nothing on this device");

        var statuses = new List<JinagaStatus>();
        j.OnStatusChanged += status => statuses.Add(status);

        await j.Push();

        // Nothing could be sent, so the queue length stays at zero throughout.
        // Before the held length existed, that was the whole report, and an
        // application could not tell this from an empty queue.
        UploadedHashes().Should().BeEmpty();
        (await QueuedHashes(store)).Should().Equal(j.Hash(siteA), j.Hash(siteB));
        statuses.Should().OnlyContain(status => status.QueueLength == 0);
        statuses.Should().Contain(status => status.HeldLength == 2);
    }

    [Fact]
    public async Task GetUnsentFacts_WithADescendantOfAHeldFact_CountsTheDescendantAsHeldAndAsThisUsers()
    {
        var j = GivenClient(GivenStore());

        identity.SetCurrentUser(UserA);
        var site = await j.Fact(new Site("a.example.com"));
        await j.Fact(new Site("b.example.com"));
        identity.SetCurrentUser(UserB);
        await j.Fact(new Post(site, "2026-11-28T00:00:00Z"));

        var unsent = await j.GetUnsentFacts();

        unsent.Total.Should().Be(3);
        // The post is this user's own, but it cannot travel without its
        // predecessor, which is held. A count that left it out would promise a
        // user that logging out costs them nothing.
        unsent.Held.Should().Be(3);
        unsent.SavedByCurrentUser.Should().Be(1);
    }

    /// <summary>
    /// A Jinaga.User is never held, because anyone may introduce one and the replicator signs
    /// it with nobody's key. It is still waiting to upload, so it is counted in the total.
    /// </summary>
    [Fact]
    public async Task GetUnsentFacts_WithAUserFactAnotherUserIntroduced_CountsItInTheTotalAndNotAsHeld()
    {
        var store = GivenStore();
        var j = GivenClient(store);
        var x = new User("---FAKE PUBLIC KEY X---");

        // A, offline, adds a new user to a project, then logs out. The queue
        // holds four facts: the user fact for A, the project, the user fact for
        // X, and the membership.
        identity.SetCurrentUser(UserA);
        var project = await j.Fact(new RoutingProject(UserA, "Apollo"));
        var membership = await j.Fact(new RoutingMembership(project, x));

        // Still A's session. A saved all four, user facts included, so the count of
        // what A saved is an upper bound on what logging out costs them rather than
        // the exact figure: two of these four travel under anyone.
        var whileTheSaverIsLoggedIn = await j.GetUnsentFacts();
        whileTheSaverIsLoggedIn.SavedByCurrentUser.Should().Be(4);
        whileTheSaverIsLoggedIn.Held.Should().Be(0);

        identity.SetCurrentUser(UserB);

        var unsent = await j.GetUnsentFacts();

        unsent.Total.Should().Be(4);
        unsent.Held.Should().Be(2, "the two user facts are not held, so only the project and the membership are");

        await j.Push();

        UploadedHashes().Should().Contain(j.Hash(UserA));
        UploadedHashes().Should().Contain(j.Hash(x));
        (await QueuedHashes(store)).Should().Equal(j.Hash(project), j.Hash(membership));

        var afterPush = await j.GetUnsentFacts();
        afterPush.Total.Should().Be(2, "the user facts are counted until they are sent");
        afterPush.Held.Should().Be(2);
    }

    [Fact]
    public async Task GetUnsentFacts_WhenTheSaverReturnsAndTheQueueDrains_CountsNothing()
    {
        var store = GivenStore();
        var j = GivenClient(store);

        identity.SetCurrentUser(UserA);
        await j.Fact(new Site("a.example.com"));
        await j.Fact(new Site("b.example.com"));
        identity.SetCurrentUser(UserB);
        await j.Push();

        var statuses = new List<JinagaStatus>();
        j.OnStatusChanged += status => statuses.Add(status);

        identity.SetCurrentUser(UserA);
        await j.Push();

        (await QueuedHashes(store)).Should().BeEmpty();
        var unsent = await j.GetUnsentFacts();
        unsent.Total.Should().Be(0);
        unsent.Held.Should().Be(0);
        unsent.SavedByCurrentUser.Should().Be(0);
        statuses.Last().HeldLength.Should().Be(0);
        statuses.Last().QueueLength.Should().Be(0);
    }

    [Fact]
    public async Task GetUnsentFacts_WithAFactSavedWhileNobodyWasLoggedIn_CountsItInTheTotalOnly()
    {
        var j = GivenClient(GivenStore());

        await j.Fact(new Site("nobody.example.com"));
        identity.SetCurrentUser(UserA);
        await j.Fact(new Site("a.example.com"));

        var whileALoggedIn = await j.GetUnsentFacts();
        whileALoggedIn.Total.Should().Be(2);
        whileALoggedIn.Held.Should().Be(0);
        whileALoggedIn.SavedByCurrentUser.Should().Be(1, "the fact nobody saved is not this user's to lose");

        identity.SetCurrentUser(UserB);

        var whileBLoggedIn = await j.GetUnsentFacts();
        whileBLoggedIn.Total.Should().Be(2);
        whileBLoggedIn.Held.Should().Be(1, "only the fact A saved is held; the one nobody saved goes under anyone");
        whileBLoggedIn.SavedByCurrentUser.Should().Be(0);

        // The classification the counts came from is the one that routes the
        // queue, so the fact nobody saved is the one that leaves.
        await j.Push();
        var afterPush = await j.GetUnsentFacts();
        afterPush.Total.Should().Be(1);
        afterPush.Held.Should().Be(1);
    }

    [Fact]
    public async Task GetUnsentFacts_WithAStoreThatOverridesNoneOfTheSaverMembers_CountsOnlyTheTotal()
    {
        var store = new StoreWithoutSaverRouting(GivenStore());
        var j = Track(new JinagaClient(
            store,
            network,
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            new JinagaClientOptions
            {
                HttpAuthenticationProvider = identity,
                QueueProcessingDelay = DelayLongerThanAnyTest
            }));

        identity.SetCurrentUser(UserA);
        await j.Fact(new Site("a.example.com"));
        identity.SetCurrentUser(UserB);
        await j.Fact(new Site("b.example.com"));

        var unsent = await j.GetUnsentFacts();

        unsent.Total.Should().Be(2);
        unsent.Held.Should().Be(0, "such a store records no savers, so it sends everything");
        unsent.SavedByCurrentUser.Should().Be(0);

        // And the routing agrees: the whole queue drains under whoever is here.
        await j.Push();
        var afterPush = await j.GetUnsentFacts();
        afterPush.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetUnsentFacts_WithANetworkThatFailsEveryCall_Answers()
    {
        var failingNetwork = new FailingFakeNetwork();
        var j = Track(new JinagaClient(
            GivenStore(),
            failingNetwork,
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            new JinagaClientOptions
            {
                HttpAuthenticationProvider = identity,
                QueueProcessingDelay = DelayLongerThanAnyTest
            }));

        identity.SetCurrentUser(UserA);
        await j.Fact(new Site("a.example.com"));
        identity.SetCurrentUser(UserB);

        var unsent = await j.GetUnsentFacts();

        unsent.Total.Should().Be(1);
        unsent.Held.Should().Be(1);
        unsent.SavedByCurrentUser.Should().Be(0);
        failingNetwork.SaveCallCount.Should().Be(0, "the counts come from the store, so an offline device can ask for them");
    }

    private SQLiteStore GivenStore()
    {
        return new SQLiteStore(DatabasePath, NullLoggerFactory.Instance);
    }

    private JinagaClient GivenClient(SQLiteStore store)
    {
        var options = new JinagaClientOptions
        {
            HttpAuthenticationProvider = identity,
            QueueProcessingDelay = DelayLongerThanAnyTest
        };
        return Track(new JinagaClient(
            store,
            network,
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            options));
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
