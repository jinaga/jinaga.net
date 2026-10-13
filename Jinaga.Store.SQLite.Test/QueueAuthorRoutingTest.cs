using Jinaga.Projections;
using Jinaga.Store.SQLite.Test.Fakes;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test;

/// <summary>
/// The same queue routing as in Jinaga.Test, against the store that a mobile
/// application actually uses. The authorship record lives in the database here,
/// so it has to survive the application being closed and opened again: a user
/// who creates facts offline and does not come back until tomorrow is the
/// ordinary case, not the edge case.
/// </summary>
public class QueueAuthorRoutingTest
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
        "QueueAuthorRoutingTest.db");

    private readonly ITestOutputHelper output;
    private readonly FakeNetwork network;
    private readonly FakeIdentitySource identity;

    public QueueAuthorRoutingTest(ITestOutputHelper output)
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
    public async Task Push_WhenTheAuthorLogsBackIn_SendsWhatWasHeldWithoutANewSave()
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
    public async Task Push_AfterTheStoreIsClosedAndOpenedAgain_StillHoldsTheFactForItsAuthor()
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

    private JinagaClient GivenClient(SQLiteStore store)
    {
        var options = new JinagaClientOptions
        {
            HttpAuthenticationProvider = identity,
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
    /// What the queue still holds, whoever authored it, in the order it was
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
