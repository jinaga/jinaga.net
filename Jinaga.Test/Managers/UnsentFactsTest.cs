#nullable enable

using Jinaga.Projections;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit.Abstractions;

namespace Jinaga.Test.Managers;

/// <summary>
/// The outbound queue holds a fact another user saved until one of its savers logs in on
/// this device again, which may be never. The client used to say nothing about such a fact:
/// the status reported only what the next pass could send, so a queue holding nothing but
/// held facts looked exactly like an empty one.
///
/// These counts are what an application needs in order to warn a user before logging them
/// out, and to show that the device is holding work somebody else saved. Each one is counted
/// from the same classification of the queue that decides what a save sends, so a count
/// cannot disagree with the routing: every test here checks the counts against what the
/// push actually did.
/// </summary>
public class UnsentFactsTest
{
    /// <summary>
    /// Long enough that the debounce never elapses during a test, so the only
    /// thing that sends the queue is an explicit Push.
    /// </summary>
    private const int DelayLongerThanAnyTest = 60000;

    private static readonly User UserA = new User("---FAKE PUBLIC KEY A---");
    private static readonly User UserB = new User("---FAKE PUBLIC KEY B---");

    private readonly ITestOutputHelper output;

    public UnsentFactsTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task GetUnsentFacts_WithTheSaverStillLoggedIn_CountsTheirFactsAsTheirsAndHoldsNothing()
    {
        var (j, _, _, identity) = CreateClient();

        identity.SetCurrentUser(UserA);
        await j.Fact(new School(Guid.NewGuid()));
        await j.Fact(new School(Guid.NewGuid()));

        var unsent = await j.GetUnsentFacts();

        unsent.Total.Should().Be(2);
        unsent.Held.Should().Be(0, "the user who saved them is the one logged in");
        unsent.SavedByCurrentUser.Should().Be(2, "these are the facts that become held if this user logs out");
    }

    [Fact]
    public async Task GetUnsentFacts_AfterAnotherUserLogsIn_CountsTheFirstUsersFactsAsHeld()
    {
        var (j, store, network, identity) = CreateClient();

        identity.SetCurrentUser(UserA);
        var schoolA = await j.Fact(new School(Guid.NewGuid()));
        var schoolB = await j.Fact(new School(Guid.NewGuid()));
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
        UploadedHashes(network).Should().BeEmpty();
        QueuedHashes(store).Should().Equal(j.Hash(schoolA), j.Hash(schoolB));
        statuses.Should().OnlyContain(status => status.QueueLength == 0);
        statuses.Should().Contain(status => status.HeldLength == 2);
    }

    [Fact]
    public async Task GetUnsentFacts_WithADescendantOfAHeldFact_CountsTheDescendantAsHeldAndAsThisUsers()
    {
        var (j, _, _, identity) = CreateClient();

        identity.SetCurrentUser(UserA);
        var school = await j.Fact(new School(Guid.NewGuid()));
        var otherSchool = await j.Fact(new School(Guid.NewGuid()));
        identity.SetCurrentUser(UserB);
        await j.Fact(new Course(school, "Math 101"));

        var unsent = await j.GetUnsentFacts();

        unsent.Total.Should().Be(3);
        // The course is this user's own, but it cannot travel without its
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
        var (j, store, network, identity) = CreateClient();
        var x = new User("---FAKE PUBLIC KEY X---");

        // A, offline, adds a new user to a project, then logs out. The queue
        // holds four facts: the user fact for A, the project, the user fact for
        // X, and the membership.
        identity.SetCurrentUser(UserA);
        var project = await j.Fact(new RoutingProject(UserA, Guid.NewGuid()));
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

        UploadedHashes(network).Should().Contain(j.Hash(UserA));
        UploadedHashes(network).Should().Contain(j.Hash(x));
        QueuedHashes(store).Should().Equal(j.Hash(project), j.Hash(membership));

        var afterPush = await j.GetUnsentFacts();
        afterPush.Total.Should().Be(2, "the user facts are counted until they are sent");
        afterPush.Held.Should().Be(2);
    }

    [Fact]
    public async Task GetUnsentFacts_WhenTheSaverReturnsAndTheQueueDrains_CountsNothing()
    {
        var (j, store, _, identity) = CreateClient();

        identity.SetCurrentUser(UserA);
        await j.Fact(new School(Guid.NewGuid()));
        await j.Fact(new School(Guid.NewGuid()));
        identity.SetCurrentUser(UserB);
        await j.Push();

        var statuses = new List<JinagaStatus>();
        j.OnStatusChanged += status => statuses.Add(status);

        identity.SetCurrentUser(UserA);
        await j.Push();

        QueuedHashes(store).Should().BeEmpty();
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
        var (j, _, _, identity) = CreateClient();

        var unclaimed = await j.Fact(new School(Guid.NewGuid()));
        identity.SetCurrentUser(UserA);
        await j.Fact(new School(Guid.NewGuid()));

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
        _ = unclaimed;
    }

    [Fact]
    public async Task GetUnsentFacts_WithAStoreThatOverridesNoneOfTheSaverMembers_CountsOnlyTheTotal()
    {
        var store = new StoreWithoutSaverRouting(new MemoryStore());
        var network = new FakeNetwork(output);
        var identity = new FakeIdentitySource();
        network.CurrentUser = () => identity.CurrentUser;
        var j = new JinagaClient(
            store,
            network,
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            new JinagaClientOptions
            {
                HttpAuthenticationProvider = identity,
                QueueProcessingDelay = DelayLongerThanAnyTest
            });

        identity.SetCurrentUser(UserA);
        await j.Fact(new School(Guid.NewGuid()));
        identity.SetCurrentUser(UserB);
        await j.Fact(new School(Guid.NewGuid()));

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
        var store = new PersistentMemoryStore();
        var network = new FailingFakeNetwork();
        var identity = new FakeIdentitySource();
        var j = new JinagaClient(
            store,
            network,
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            new JinagaClientOptions
            {
                HttpAuthenticationProvider = identity,
                QueueProcessingDelay = DelayLongerThanAnyTest
            });

        identity.SetCurrentUser(UserA);
        await j.Fact(new School(Guid.NewGuid()));
        identity.SetCurrentUser(UserB);

        var unsent = await j.GetUnsentFacts();

        unsent.Total.Should().Be(1);
        unsent.Held.Should().Be(1);
        unsent.SavedByCurrentUser.Should().Be(0);
        network.SaveCallCount.Should().Be(0, "the counts come from the store, so an offline device can ask for them");
    }

    private (JinagaClient j, PersistentMemoryStore store, FakeNetwork network, FakeIdentitySource identity) CreateClient()
    {
        var store = new PersistentMemoryStore();
        var network = new FakeNetwork(output);
        var identity = new FakeIdentitySource();
        network.CurrentUser = () => identity.CurrentUser;
        var options = new JinagaClientOptions
        {
            HttpAuthenticationProvider = identity,
            QueueProcessingDelay = DelayLongerThanAnyTest
        };
        var j = new JinagaClient(
            store,
            network,
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            options);
        return (j, store, network, identity);
    }

    private static IEnumerable<string> UploadedHashes(FakeNetwork network)
    {
        return network.UploadedGraph.FactReferences
            .Select(reference => reference.Hash)
            .ToImmutableList();
    }

    private static IEnumerable<string> QueuedHashes(PersistentMemoryStore store)
    {
        var queue = store.GetQueue().GetAwaiter().GetResult();
        return queue.Facts
            .Select(fact => fact.Reference.Hash)
            .ToImmutableList();
    }
}
