using Jinaga.Facts;
using Jinaga.Observers;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Jinaga.Test.Specifications;

/// <summary>
/// A fact graph answers a specification only through its graph form, and a specification
/// has one only if every part of it is something the graph evaluates. These hold the line
/// at both ends: a shape with no graph form goes to the store, and one that reaches
/// <see cref="Specification.Execute"/> anyway is refused rather than answered wrongly.
/// </summary>
public class GraphSpecificationTest
{
    private readonly MemoryStore store = new MemoryStore();
    private readonly JinagaClient j;

    public GraphSpecificationTest(Xunit.Abstractions.ITestOutputHelper output)
    {
        j = new JinagaClient(
            store,
            new FakeNetwork(output),
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            new JinagaClientOptions());
    }

    [Fact]
    public async Task NegativeConditionOnAMatch_HasNoGraphFormAndAnswersFromTheStore()
    {
        var world = await GivenAWorld();

        var specification = Given<Mirror>.Match((m, facts) =>
            from a in facts.OfType<Account>()
            where m.account == a
            where !facts.Any<Activated>(activated => activated.account == a)
            select a
        );
        specification.CanRunOnGraph.Should().BeFalse();

        (await j.Query(specification, world.ActiveMirror)).Should().BeEmpty();
        (await j.Query(specification, world.IdleMirror)).Should().Equal(world.IdleAccount);
        await QueryShouldMatchTheStore(specification, world.ActiveMirror);
        await QueryShouldMatchTheStore(specification, world.IdleMirror);
    }

    [Fact]
    public async Task PositiveConditionOnAMatch_HasNoGraphFormAndAnswersFromTheStore()
    {
        var world = await GivenAWorld();

        var specification = Given<Mirror>.Match((m, facts) =>
            from a in facts.OfType<Account>()
            where m.account == a
            where facts.Any<Activated>(activated => activated.account == a)
            select a
        );
        specification.CanRunOnGraph.Should().BeFalse();

        (await j.Query(specification, world.ActiveMirror)).Should().Equal(world.ActiveAccount);
        (await j.Query(specification, world.IdleMirror)).Should().BeEmpty();
        await QueryShouldMatchTheStore(specification, world.ActiveMirror);
        await QueryShouldMatchTheStore(specification, world.IdleMirror);
    }

    [Fact]
    public async Task ConditionInsideACollection_HasNoGraphFormAndAnswersFromTheStore()
    {
        var world = await GivenAWorld();

        // Every match here walks predecessors only. The condition sits inside the
        // collection, which is where a gate that looked only at the outer matches would
        // stop looking.
        var activatedAccount = Given<Mirror>.Match((m, facts) =>
            from a in facts.OfType<Account>()
            where m.account == a
            where facts.Any<Activated>(activated => activated.account == a)
            select a
        );
        var specification = Given<Mirror>.Match((m, facts) =>
            from a in facts.OfType<Account>()
            where m.account == a
            select new MirrorAccounts(a, facts.Observable(m, activatedAccount))
        );
        specification.CanRunOnGraph.Should().BeFalse();

        var active = (await j.Query(specification, world.ActiveMirror)).Should().ContainSingle().Subject;
        active.account.Should().Be(world.ActiveAccount);
        active.activated.Should().Equal(world.ActiveAccount);

        var idle = (await j.Query(specification, world.IdleMirror)).Should().ContainSingle().Subject;
        idle.account.Should().Be(world.IdleAccount);
        idle.activated.Should().BeEmpty();
    }

    [Fact]
    public void ConditionOnAGiven_HasNoGraphForm()
    {
        // The C# query DSL puts every condition on a match, so this is built from
        // Specification objects, as the inverses this library computes for itself are.
        GivenWithACondition.CanRunOnGraph.Should().BeFalse();
    }

    [Fact]
    public async Task SuccessorStep_HasNoGraphFormAndAnswersFromTheStore()
    {
        var world = await GivenAWorld();

        var specification = Given<Account>.Match((a, facts) =>
            from m in facts.OfType<Mirror>()
            where m.account == a
            select m
        );
        specification.CanRunOnGraph.Should().BeFalse();

        (await j.Query(specification, world.ActiveAccount)).Should().Equal(world.ActiveMirror);
        await QueryShouldMatchTheStore(specification, world.ActiveAccount);
    }

    [Fact]
    public async Task SignerProjection_HasNoGraphFormAndAnswersFromTheStore()
    {
        var world = await GivenAWorld();

        var specification = Given<Mirror>.Match((m, facts) =>
            from a in facts.OfType<Account>()
            where m.account == a
            select new AccountSigners(a, facts.SignersOf(a))
        );
        specification.CanRunOnGraph.Should().BeFalse();

        var result = (await j.Query(specification, world.ActiveMirror)).Should().ContainSingle().Subject;
        result.account.Should().Be(world.ActiveAccount);
        result.signers.Should().BeEmpty();
    }

    [Fact]
    public async Task DateLearnedProjection_HasNoGraphFormAndAnswersFromTheStore()
    {
        var world = await GivenAWorld();

        var specification = Given<Mirror>.Match((m, facts) =>
            from a in facts.OfType<Account>()
            where m.account == a
            select new AccountLearned(a, facts.DateLearned(a))
        );
        specification.CanRunOnGraph.Should().BeFalse();

        var result = (await j.Query(specification, world.ActiveMirror)).Should().ContainSingle().Subject;
        result.account.Should().Be(world.ActiveAccount);
        result.learned.Should().NotBe(default(DateTime));
    }

    [Fact]
    public async Task ConditionOnAMatch_IsRefusedByExecuteRatherThanSkipped()
    {
        var world = await GivenAWorld();

        var specification = Given<Mirror>.Match((m, facts) =>
            from a in facts.OfType<Account>()
            where m.account == a
            where !facts.Any<Activated>(activated => activated.account == a)
            select a
        );

        // The account is activated, so the store returns nothing. Skipping the condition
        // would return the account instead.
        var graph = j.Graph(world.ActiveMirror);
        var givenTuple = FactReferenceTuple.Empty
            .Add(specification.Givens.Single().Label.Name, graph.Last);
        (await store.Read(givenTuple, specification, CancellationToken.None)).Should().BeEmpty();

        specification.Invoking(s => s.Execute(givenTuple, graph))
            .Should().Throw<ArgumentException>()
            .WithMessage("*existential condition*");
    }

    [Fact]
    public async Task SuccessorStep_IsRefusedByExecuteRatherThanBoundToTheRightHandSide()
    {
        var world = await GivenAWorld();

        var specification = Given<Account>.Match((a, facts) =>
            from m in facts.OfType<Mirror>()
            where m.account == a
            select m
        );

        // Treating the successor step as an empty path would bind 'm' to the account.
        var graph = j.Graph(world.ActiveMirror);
        var givenTuple = FactReferenceTuple.Empty
            .Add(specification.Givens.Single().Label.Name, j.Graph(world.ActiveAccount).Last);

        specification.Invoking(s => s.Execute(givenTuple, graph))
            .Should().Throw<ArgumentException>()
            .WithMessage("*successors*");
    }

    [Fact]
    public async Task TwoPredecessorPaths_RunOnTheGraphAndAgreeWithTheStoreWhenTheyAgree()
    {
        var world = await GivenAWorld();
        var ticket = await j.Fact(new Ticket(world.Tenant, world.ActiveAccount, "same tenant"));

        var specification = TicketTenantReachedBothWays;
        specification.CanRunOnGraph.Should().BeTrue();

        await GraphShouldBuildTheSameProductsAsTheStore(specification, ticket);
        (await j.Query(specification, ticket)).Should().Equal(world.Tenant);
    }

    [Fact]
    public async Task TwoPredecessorPaths_RunOnTheGraphAndAgreeWithTheStoreWhenTheyDisagree()
    {
        var world = await GivenAWorld();
        var otherTenant = await j.Fact(new Tenant("other"));
        var reporter = await j.Fact(new Account(otherTenant, "outsider"));
        var ticket = await j.Fact(new Ticket(world.Tenant, reporter, "crossed tenants"));

        var specification = TicketTenantReachedBothWays;
        specification.CanRunOnGraph.Should().BeTrue();

        await GraphShouldBuildTheSameProductsAsTheStore(specification, ticket);
        (await j.Query(specification, ticket)).Should().BeEmpty();
    }

    [Fact]
    public void AuthorizationRule_WhoseGivenCarriesAConditionIsRefusedWhenItIsDefined()
    {
        Action defining = () => new AuthorizationRuleSpecification(GivenWithACondition);

        defining.Should().Throw<InvalidOperationException>()
            .WithMessage("*'u'*existential condition*");
    }

    /// <summary>
    /// <code>
    /// (u: Jinaga.User [ !E { m: Mirror.Mirror [ m = u->retracted: Mirror.Mirror ] } ]) { } => u
    /// </code>
    /// A given that carries a condition, with a projection an authorization rule accepts.
    /// </summary>
    private static Specification GivenWithACondition => new Specification(
        ImmutableList.Create(
            new SpecificationGiven(
                new Label("u", "Jinaga.User"),
                ImmutableList.Create(
                    new ExistentialCondition(false, ImmutableList.Create(
                        new Match(
                            new Label("m", "Mirror.Mirror"),
                            ImmutableList.Create(
                                new PathCondition(
                                    ImmutableList<Role>.Empty,
                                    "u",
                                    ImmutableList.Create(new Role("retracted", "Mirror.Mirror")))),
                            ImmutableList<ExistentialCondition>.Empty)))))),
        ImmutableList<Match>.Empty,
        new SimpleProjection("u", typeof(User)));

    /// <summary>
    /// The ticket's tenant, reached both directly and through its reporter: one match with
    /// two predecessor-only paths, which hold together only when the reporter belongs to
    /// the tenant the ticket names.
    /// </summary>
    private static Specification<Ticket, Tenant> TicketTenantReachedBothWays =>
        Given<Ticket>.Match((ticket, facts) =>
            from t in facts.OfType<Tenant>()
            where ticket.tenant == t
            where ticket.reporter.tenant == t
            select t
        );

    private async Task<World> GivenAWorld()
    {
        var tenant = await j.Fact(new Tenant("acme"));
        var activeAccount = await j.Fact(new Account(tenant, "active"));
        var idleAccount = await j.Fact(new Account(tenant, "idle"));
        await j.Fact(new Activated(activeAccount));
        return new World(
            tenant,
            activeAccount,
            idleAccount,
            await j.Fact(new Mirror(activeAccount, "active.example.com")),
            await j.Fact(new Mirror(idleAccount, "idle.example.com")));
    }

    private record World(
        Tenant Tenant,
        Account ActiveAccount,
        Account IdleAccount,
        Mirror ActiveMirror,
        Mirror IdleMirror);

    /// <summary>
    /// The client's answer is the store's answer, compared as the fact references the
    /// store's products carry.
    /// </summary>
    private async Task QueryShouldMatchTheStore<TFact, TProjection>(
        Specification<TFact, TProjection> specification, TFact given)
        where TFact : class
        where TProjection : class
    {
        var fromQuery = await j.Query(specification, given);
        var fromStore = await ReadFromStore(specification, given);
        fromQuery.Select(result => j.Hash(result))
            .Should().Equal(fromStore.Select(product =>
                product.GetFactReference(LabelOfProjection(specification)).Hash));
    }

    /// <summary>
    /// The graph builds the products the store builds, element by element.
    /// </summary>
    private async Task GraphShouldBuildTheSameProductsAsTheStore<TFact, TProjection>(
        Specification<TFact, TProjection> specification, TFact given)
        where TFact : class
    {
        var graph = j.Graph(given);
        var givenTuple = FactReferenceTuple.Empty
            .Add(specification.Givens.Single().Label.Name, graph.Last);

        var fromGraph = specification.Execute(givenTuple, graph);
        var fromStore = await store.Read(givenTuple, specification, CancellationToken.None);

        fromGraph.Count.Should().Be(fromStore.Count);
        foreach (var (graphProduct, storeProduct) in fromGraph.Zip(fromStore, (g, s) => (g, s)))
        {
            graphProduct.Names.Should().BeEquivalentTo(storeProduct.Names);
            foreach (var name in storeProduct.Names)
            {
                graphProduct.GetElement(name).Should().Be(storeProduct.GetElement(name), because: name);
            }
        }
    }

    private Task<ImmutableList<Product>> ReadFromStore<TFact, TProjection>(
        Specification<TFact, TProjection> specification, TFact given)
        where TFact : class
    {
        var graph = j.Graph(given);
        return store.Read(
            FactReferenceTuple.Empty.Add(specification.Givens.Single().Label.Name, graph.Last),
            specification,
            CancellationToken.None);
    }

    private static string LabelOfProjection<TFact, TProjection>(
        Specification<TFact, TProjection> specification)
        where TFact : class
    {
        return ((SimpleProjection)specification.Projection).Tag;
    }

    private record MirrorAccounts(Account account, IObservableCollection<Account> activated);

    private record AccountSigners(Account account, IEnumerable<User> signers);

    private record AccountLearned(Account account, DateTime learned);
}
