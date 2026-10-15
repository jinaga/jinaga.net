#nullable enable

using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;

namespace Jinaga.Test.Managers;

/// <summary>
/// A client given <see cref="JinagaClientOptions.AuthorizationRules"/> checks each save
/// against them as the logged-in user, before anything reaches the store. A fact the
/// replicator would refuse is refused here, where the application can still tell the user,
/// instead of sitting in the queue or stalling an upload.
/// </summary>
public class EarlyAuthorizationTest
{
    /// <summary>
    /// Long enough that the debounce never elapses during a test, so nothing is sent
    /// unless a test pushes.
    /// </summary>
    private const int DelayLongerThanAnyTest = 60000;

    private static readonly User UserA = new User("---FAKE PUBLIC KEY A---");
    private static readonly User UserB = new User("---FAKE PUBLIC KEY B---");

    private readonly ITestOutputHelper output;

    public EarlyAuthorizationTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task Fact_WhenTheRulesRefuseTheLoggedInUser_ThrowsAndLeavesNothingBehind()
    {
        var (j, store, _, identity) = CreateClient(EarlyAuthorizationModel.Rules);
        identity.SetCurrentUser(UserA);

        // Only a project's creator may create it, and A is not B.
        var prototype = new EarlyProject(UserB, "B's project");
        var references = j.Graph(prototype).FactReferences;

        var save = () => j.Fact(prototype);

        (await save.Should().ThrowAsync<Jinaga.Authorization.AuthorizationException>())
            .Which.FactType.Should().Be("Test.EarlyAuthorization.Project");
        (await store.ListKnown(references)).Should().BeEmpty();
        (await store.GetQueue()).Facts.Should().BeEmpty();
        (await store.LoadSavers(references, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Fact_WhenTheRulesAuthorizeTheLoggedInUser_SavesAndQueues()
    {
        var (j, store, _, identity) = CreateClient(EarlyAuthorizationModel.Rules);
        identity.SetCurrentUser(UserA);

        var project = await j.Fact(new EarlyProject(UserA, "A's project"));

        var reference = j.Graph(project).Last;
        (await store.ListKnown(ImmutableList.Create(reference))).Should().Equal(reference);
        (await store.GetQueue()).Facts.Select(fact => fact.Reference).Should().Contain(reference);
    }

    [Fact]
    public async Task Fact_OfATypeWithNoRule_IsRefusedRatherThanHeldInTheQueue()
    {
        var (j, store, _, identity) = CreateClient(EarlyAuthorizationModel.Rules);
        identity.SetCurrentUser(UserA);

        var prototype = new EarlyNote("No rule names this type");
        var references = j.Graph(prototype).FactReferences;

        var save = () => j.Fact(prototype);

        await save.Should().ThrowAsync<Jinaga.Authorization.AuthorizationException>();
        (await store.ListKnown(references)).Should().BeEmpty();
        (await store.GetQueue()).Facts.Should().BeEmpty();
    }

    [Fact]
    public async Task LocalFact_WhenTheRulesRefuseTheLoggedInUser_Throws()
    {
        var (j, store, _, identity) = CreateClient(EarlyAuthorizationModel.Rules);
        identity.SetCurrentUser(UserA);

        var prototype = new EarlyProject(UserB, "B's project");
        var references = j.Graph(prototype).FactReferences;

        var save = () => j.Local.Fact(prototype);

        await save.Should().ThrowAsync<Jinaga.Authorization.AuthorizationException>();
        (await store.ListKnown(references)).Should().BeEmpty();
    }

    /// <summary>
    /// With nobody logged in there is no user to check as. The save goes ahead, and
    /// routing decides who it is uploaded under.
    /// </summary>
    [Fact]
    public async Task Fact_WithNobodyLoggedIn_IsSavedWithoutBeingChecked()
    {
        var (j, store, _, _) = CreateClient(EarlyAuthorizationModel.Rules);

        var project = await j.Fact(new EarlyProject(UserB, "B's project"));

        var reference = j.Graph(project).Last;
        (await store.ListKnown(ImmutableList.Create(reference))).Should().Equal(reference);
        (await store.GetQueue()).Facts.Select(fact => fact.Reference).Should().Contain(reference);
    }

    [Fact]
    public async Task Fact_WithoutRules_IsSavedWhoeverIsLoggedIn()
    {
        var (j, store, _, identity) = CreateClient(rules: null);
        identity.SetCurrentUser(UserA);

        var project = await j.Fact(new EarlyProject(UserB, "B's project"));
        var note = await j.Fact(new EarlyNote("No rule names this type"));

        var references = ImmutableList.Create(j.Graph(project).Last, j.Graph(note).Last);
        (await store.ListKnown(references)).Should().BeEquivalentTo(references);
    }

    /// <summary>
    /// The documented caveat. A rule that walks to successors of the new fact's
    /// predecessors reads the local store, and this replica has not loaded the membership
    /// that admits B. The replicator, which has it, would accept the task; the client
    /// refuses it.
    /// </summary>
    [Fact]
    public async Task Caveat_ARuleThatWalksSuccessors_RefusesWhenTheAuthorizingFactIsNotInTheLocalStore()
    {
        var (j, store, _, identity) = CreateClient(EarlyAuthorizationModel.Rules);
        var project = new EarlyProject(UserA, "A's project");
        await SaveAsThoughSynced(j, store, project);

        identity.SetCurrentUser(UserB);
        var save = () => j.Fact(new EarlyTask(project, "Write the report"));

        await save.Should().ThrowAsync<Jinaga.Authorization.AuthorizationException>();
    }

    /// <summary>
    /// The remedy for the caveat: once the membership is in the local store, the same
    /// save goes through.
    /// </summary>
    [Fact]
    public async Task Caveat_ARuleThatWalksSuccessors_AuthorizesOnceTheAuthorizingFactIsLoaded()
    {
        var (j, store, _, identity) = CreateClient(EarlyAuthorizationModel.Rules);
        var project = new EarlyProject(UserA, "A's project");
        await SaveAsThoughSynced(j, store, project, new EarlyMembership(project, UserB));

        identity.SetCurrentUser(UserB);
        var task = await j.Fact(new EarlyTask(project, "Write the report"));

        var reference = j.Graph(task).Last;
        (await store.ListKnown(ImmutableList.Create(reference))).Should().Equal(reference);
    }

    /// <summary>
    /// A fact made in a single-use block is signed by the single-use principal, and the
    /// replicator authorizes it by that signature as well as by the uploader. The client
    /// checks as one user, so it does not check these saves rather than refuse one the
    /// replicator would accept.
    /// </summary>
    [Fact]
    public async Task SingleUse_AFactThePrincipalAuthors_IsNotRefusedAsTheLoggedInUser()
    {
        var (j, store, _, identity) = CreateClient(EarlyAuthorizationModel.Rules);
        identity.SetCurrentUser(UserA);

        var project = await j.SingleUse(principal =>
            j.Fact(new EarlyProject(principal, "The principal's project")));

        var reference = j.Graph(project).Last;
        (await store.ListKnown(ImmutableList.Create(reference))).Should().Equal(reference);
    }

    /// <summary>
    /// Facts this replica learned from elsewhere, written straight to the store as a
    /// sync would, without being authored or checked.
    /// </summary>
    private static async Task SaveAsThoughSynced(JinagaClient j, PersistentMemoryStore store, params object[] facts)
    {
        var graph = facts
            .Select(fact => j.Graph(fact))
            .Aggregate(FactGraph.Empty, (all, one) => all.AddGraph(one));
        await store.Save(graph, false, CancellationToken.None);
    }

    private (JinagaClient j, PersistentMemoryStore store, FakeNetwork network, FakeIdentitySource identity) CreateClient(AuthorizationRules? rules)
    {
        var store = new PersistentMemoryStore();
        var network = new FakeNetwork(output);
        var identity = new FakeIdentitySource();
        network.CurrentUser = () => identity.CurrentUser;
        var options = new JinagaClientOptions
        {
            HttpAuthenticationProvider = identity,
            AuthorizationRules = rules,
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
}

[FactType("Test.EarlyAuthorization.Project")]
public record EarlyProject(User creator, string name);

[FactType("Test.EarlyAuthorization.Membership")]
public record EarlyMembership(EarlyProject project, User member);

[FactType("Test.EarlyAuthorization.Task")]
public record EarlyTask(EarlyProject project, string title);

[FactType("Test.EarlyAuthorization.Note")]
public record EarlyNote(string text);

/// <summary>
/// The rules a replicator for this model would run. A project's creator may create it and
/// add members; a member may add tasks, which is a rule that walks from the task's project
/// to its memberships, its successors. Notes have no rule, so nobody may create one.
/// </summary>
public static class EarlyAuthorizationModel
{
    public static AuthorizationRules Rules { get; } = AuthorizationRules.Build(rules => rules
        .Any<User>()
        .Type<EarlyProject>(project => project.creator)
        .Type<EarlyMembership>(membership => membership.project.creator)
        .Type<EarlyTask>((task, facts) =>
            from membership in facts.OfType<EarlyMembership>()
            where membership.project == task.project
            from user in facts.OfType<User>()
            where user == membership.member
            select user));
}
