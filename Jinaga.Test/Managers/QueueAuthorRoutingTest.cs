#nullable enable

using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;

namespace Jinaga.Test.Managers;

/// <summary>
/// The outbound queue is one log shared by everybody who logs in on a device,
/// and it used to upload all of it under whoever held the token at the time. A
/// user who created facts offline and logged out had them uploaded under the
/// next user to log in, signed with that user's key and attributed to them on
/// every replica, permanently.
///
/// A fact is now recorded against the user who saved it, and the queue sends it
/// only while that user is logged in. Nothing is dropped: a fact its author is
/// not there to send stays in the queue.
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

    private readonly ITestOutputHelper output;

    public QueueAuthorRoutingTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task Push_AfterAnotherUserLogsIn_HoldsTheFirstUsersFact()
    {
        var (j, store, network, identity) = CreateClient();

        identity.SetCurrentUser(UserA);
        var schoolA = await j.Fact(new School(Guid.NewGuid()));
        identity.SetCurrentUser(UserB);
        var schoolB = await j.Fact(new School(Guid.NewGuid()));

        await j.Push();

        UploadedHashes(network).Should().Contain(j.Hash(schoolB));
        UploadedHashes(network).Should().NotContain(j.Hash(schoolA));
        network.UploadedUnderUsers.Should().Equal(UserB);
        QueuedHashes(store).Should().Equal(j.Hash(schoolA));
    }

    [Fact]
    public async Task Push_WhenTheAuthorLogsBackIn_SendsWhatWasHeldWithoutANewSave()
    {
        var (j, store, network, identity) = CreateClient();

        identity.SetCurrentUser(UserA);
        var schoolA = await j.Fact(new School(Guid.NewGuid()));
        identity.SetCurrentUser(UserB);
        await j.Fact(new School(Guid.NewGuid()));
        await j.Push();

        // No fact is saved from here on. The only thing that changes is who is
        // logged in.
        identity.SetCurrentUser(UserA);
        await j.Push();

        UploadedHashes(network).Should().Contain(j.Hash(schoolA));
        network.UploadedUnderUsers.Should().Equal(UserB, UserA);
        QueuedHashes(store).Should().BeEmpty();
    }

    [Fact]
    public async Task Push_WithAFactWhoseAncestorAnotherUserQueued_HoldsItUntilTheAncestorIsSent()
    {
        var (j, store, network, identity) = CreateClient();

        identity.SetCurrentUser(UserA);
        var school = await j.Fact(new School(Guid.NewGuid()));
        identity.SetCurrentUser(UserB);
        var course = await j.Fact(new Course(school, "Math 101"));

        // The course is this user's, but its predecessor is not, and sending it
        // would carry the predecessor along under the wrong identity.
        await j.Push();
        UploadedHashes(network).Should().BeEmpty();
        QueuedHashes(store).Should().Equal(j.Hash(school), j.Hash(course));

        identity.SetCurrentUser(UserA);
        await j.Push();
        UploadedHashes(network).Should().Equal(j.Hash(school));
        QueuedHashes(store).Should().Equal(j.Hash(course));

        identity.SetCurrentUser(UserB);
        await j.Push();
        UploadedHashes(network).Should().Contain(j.Hash(course));
        QueuedHashes(store).Should().BeEmpty();
    }

    [Fact]
    public async Task Push_WithAFactSavedWhileNobodyWasLoggedIn_SendsItUnderWhoeverIsLoggedInNow()
    {
        var (j, store, network, identity) = CreateClient();

        var school = await j.Fact(new School(Guid.NewGuid()));

        identity.SetCurrentUser(UserA);
        await j.Push();

        UploadedHashes(network).Should().Equal(j.Hash(school));
        network.UploadedUnderUsers.Should().Equal(UserA);
        QueuedHashes(store).Should().BeEmpty();
    }

    [Fact]
    public async Task Push_WithAFactNobodyClaimedUnderAFactSomebodyDid_SendsThemTogether()
    {
        var (j, store, network, identity) = CreateClient();

        // Nobody is logged in, so nobody claims the school.
        var school = await j.Fact(new School(Guid.NewGuid()));
        identity.SetCurrentUser(UserA);
        var course = await j.Fact(new Course(school, "Math 101"));

        await j.Push();

        UploadedHashes(network).Should().Contain(j.Hash(school));
        UploadedHashes(network).Should().Contain(j.Hash(course));
        QueuedHashes(store).Should().BeEmpty();
    }

    [Fact]
    public async Task Push_WithAProviderThatDoesNotReportTheUser_SendsEveryQueuedFact()
    {
        var store = new PersistentMemoryStore();
        var network = new FakeNetwork(output);
        var options = new JinagaClientOptions
        {
            HttpAuthenticationProvider = new FakeAuthenticationProviderWithoutIdentity(),
            QueueProcessingDelay = DelayLongerThanAnyTest
        };
        var j = new JinagaClient(
            store,
            network,
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            options);

        var first = await j.Fact(new School(Guid.NewGuid()));
        var second = await j.Fact(new School(Guid.NewGuid()));

        await j.Push();

        UploadedHashes(network).Should().Contain(j.Hash(first));
        UploadedHashes(network).Should().Contain(j.Hash(second));
        QueuedHashes(store).Should().BeEmpty();
    }

    [Fact]
    public async Task Push_WithAStoreThatOverridesNoneOfTheNewMembers_SendsEveryQueuedFact()
    {
        var inner = new MemoryStore();
        var store = new StoreWithoutAuthorRouting(inner);
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

        identity.SetCurrentUser(UserA);
        var schoolA = await j.Fact(new School(Guid.NewGuid()));
        identity.SetCurrentUser(UserB);
        var schoolB = await j.Fact(new School(Guid.NewGuid()));

        await j.Push();

        // The store records no authors, so there is nothing to route by and the
        // queue drains the way it did before authors existed.
        UploadedHashes(network).Should().Contain(j.Hash(schoolA));
        UploadedHashes(network).Should().Contain(j.Hash(schoolB));
        var queue = await store.GetQueue();
        queue.Facts.Should().BeEmpty();
    }

    /// <summary>
    /// An IStore that overrides none of the members this change added, which is
    /// what every implementation outside this repository looks like until its
    /// author takes them up. The default implementations are what has to keep it
    /// working.
    /// </summary>
    private class StoreWithoutAuthorRouting : IStore
    {
        private readonly IStore inner;

        public StoreWithoutAuthorRouting(IStore inner)
        {
            this.inner = inner;
        }

        public bool IsPersistent => true;

        public Task<ImmutableList<Jinaga.Facts.Fact>> Save(FactGraph graph, bool queue, CancellationToken cancellationToken) =>
            inner.Save(graph, queue, cancellationToken);

        public Task<ImmutableList<Product>> Read(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken) =>
            inner.Read(givenTuple, specification, cancellationToken);

        public Task<FactGraph> Load(ImmutableList<FactReference> references, CancellationToken cancellationToken) =>
            inner.Load(references, cancellationToken);

        public Task<ImmutableDictionary<FactReference, DateTime>> LoadDatesLearned(ImmutableList<FactReference> references, CancellationToken cancellationToken) =>
            inner.LoadDatesLearned(references, cancellationToken);

        public Task<string> LoadBookmark(string feed) => inner.LoadBookmark(feed);

        public Task<ImmutableList<FactReference>> ListKnown(ImmutableList<FactReference> factReferences) =>
            inner.ListKnown(factReferences);

        public Task SaveBookmark(string feed, string bookmark) => inner.SaveBookmark(feed, bookmark);

        public Task<DateTime?> GetMruDate(string specificationHash) => inner.GetMruDate(specificationHash);

        public Task SetMruDate(string specificationHash, DateTime mruDate) => inner.SetMruDate(specificationHash, mruDate);

        public Task<QueuedFacts> GetQueue() => inner.GetQueue();

        public Task SetQueueBookmark(string bookmark) => inner.SetQueueBookmark(bookmark);

        public Task<IEnumerable<Jinaga.Facts.Fact>> GetAllFacts() => inner.GetAllFacts();

        public Task Purge(ImmutableList<Specification> purgeConditions) => inner.Purge(purgeConditions);

        public Task PurgeDescendants(FactReference purgeRoot, ImmutableList<FactReference> triggers) =>
            inner.PurgeDescendants(purgeRoot, triggers);
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

    /// <summary>
    /// What the queue still holds, whoever authored it, in the order it was
    /// queued.
    /// </summary>
    private static IEnumerable<string> QueuedHashes(PersistentMemoryStore store)
    {
        var queue = store.GetQueue().GetAwaiter().GetResult();
        return queue.Facts
            .Select(fact => fact.Reference.Hash)
            .ToImmutableList();
    }
}
