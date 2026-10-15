using Jinaga.Authorization;
using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Store.SQLite.Test.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test;

/// <summary>
/// The save check from Jinaga.Test, against the store a mobile application uses. A refused
/// save must leave nothing in the database: no fact, no outbound_queue row and no fact_saver
/// row.
/// </summary>
public class EarlyAuthorizationTest
{
    private const int DelayLongerThanAnyTest = 60000;

    private static readonly User UserA = new User("---FAKE PUBLIC KEY A---");
    private static readonly User UserB = new User("---FAKE PUBLIC KEY B---");

    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "EarlyAuthorizationTest.db");

    private readonly FakeNetwork network;
    private readonly FakeIdentitySource identity;

    public EarlyAuthorizationTest(ITestOutputHelper output)
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        network = new FakeNetwork(output);
        identity = new FakeIdentitySource();
        network.CurrentUser = () => identity.CurrentUser;
    }

    [Fact]
    public async Task Fact_WhenTheRulesRefuseTheLoggedInUser_ThrowsAndLeavesNothingBehind()
    {
        var store = GivenStore();
        var j = GivenClient(store, Rules);
        identity.SetCurrentUser(UserA);

        var prototype = new EarlyProject(UserB, "B's project");
        var references = j.Graph(prototype).FactReferences;

        var save = () => j.Fact(prototype);

        (await save.Should().ThrowAsync<AuthorizationException>())
            .Which.FactType.Should().Be("Test.EarlyAuthorization.Project");
        (await store.ListKnown(references)).Should().BeEmpty();
        (await store.GetQueue()).Facts.Should().BeEmpty();
        (await ((IStore)store).LoadSavers(references, CancellationToken.None)).Should().BeEmpty();

        await j.Unload();
    }

    [Fact]
    public async Task Fact_WhenTheRulesAuthorizeTheLoggedInUser_SavesAndQueues()
    {
        var store = GivenStore();
        var j = GivenClient(store, Rules);
        identity.SetCurrentUser(UserA);

        var project = await j.Fact(new EarlyProject(UserA, "A's project"));

        var reference = j.Graph(project).Last;
        (await store.ListKnown(ImmutableList.Create(reference))).Should().Equal(reference);
        (await store.GetQueue()).Facts.Select(fact => fact.Reference).Should().Contain(reference);

        await j.Unload();
    }

    [Fact]
    public async Task Fact_OfATypeWithNoRule_IsRefusedRatherThanHeldInTheQueue()
    {
        var store = GivenStore();
        var j = GivenClient(store, Rules);
        identity.SetCurrentUser(UserA);

        var prototype = new EarlyNote("No rule names this type");
        var references = j.Graph(prototype).FactReferences;

        var save = () => j.Fact(prototype);

        await save.Should().ThrowAsync<AuthorizationException>();
        (await store.ListKnown(references)).Should().BeEmpty();
        (await store.GetQueue()).Facts.Should().BeEmpty();

        await j.Unload();
    }

    [Fact]
    public async Task Fact_WithNobodyLoggedIn_IsSavedWithoutBeingChecked()
    {
        var store = GivenStore();
        var j = GivenClient(store, Rules);

        var project = await j.Fact(new EarlyProject(UserB, "B's project"));

        var reference = j.Graph(project).Last;
        (await store.ListKnown(ImmutableList.Create(reference))).Should().Equal(reference);
        (await store.GetQueue()).Facts.Select(fact => fact.Reference).Should().Contain(reference);

        await j.Unload();
    }

    /// <summary>
    /// The documented caveat: the membership that admits B is not in this replica's store,
    /// so a rule that walks to it refuses a task the replicator would accept.
    /// </summary>
    [Fact]
    public async Task Caveat_ARuleThatWalksSuccessors_RefusesWhenTheAuthorizingFactIsNotInTheLocalStore()
    {
        var store = GivenStore();
        var j = GivenClient(store, Rules);
        var project = new EarlyProject(UserA, "A's project");
        await ((IStore)store).Save(j.Graph(project), false, CancellationToken.None);

        identity.SetCurrentUser(UserB);
        var save = () => j.Fact(new EarlyTask(project, "Write the report"));

        await save.Should().ThrowAsync<AuthorizationException>();

        await j.Unload();
    }

    [Fact]
    public async Task Caveat_ARuleThatWalksSuccessors_AuthorizesOnceTheAuthorizingFactIsLoaded()
    {
        var store = GivenStore();
        var j = GivenClient(store, Rules);
        var project = new EarlyProject(UserA, "A's project");
        await ((IStore)store).Save(j.Graph(new EarlyMembership(project, UserB)), false, CancellationToken.None);

        identity.SetCurrentUser(UserB);
        var task = await j.Fact(new EarlyTask(project, "Write the report"));

        var reference = j.Graph(task).Last;
        (await store.ListKnown(ImmutableList.Create(reference))).Should().Equal(reference);

        await j.Unload();
    }

    private static SQLiteStore GivenStore()
    {
        return new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
    }

    private static AuthorizationRules Rules { get; } = AuthorizationRules.Build(rules => rules
        .Any<User>()
        .Type<EarlyProject>(project => project.creator)
        .Type<EarlyMembership>(membership => membership.project.creator)
        .Type<EarlyTask>((task, facts) =>
            from membership in facts.OfType<EarlyMembership>()
            where membership.project == task.project
            from user in facts.OfType<User>()
            where user == membership.member
            select user));

    private JinagaClient GivenClient(SQLiteStore store, AuthorizationRules? rules)
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
}

[FactType("Test.EarlyAuthorization.Project")]
public record EarlyProject(User creator, string name);

[FactType("Test.EarlyAuthorization.Membership")]
public record EarlyMembership(EarlyProject project, User member);

[FactType("Test.EarlyAuthorization.Task")]
public record EarlyTask(EarlyProject project, string title);

[FactType("Test.EarlyAuthorization.Note")]
public record EarlyNote(string text);
