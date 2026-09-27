using System.Collections.Immutable;
using System.Linq;
using Jinaga.Authorization;

namespace Jinaga.Test;

// Model types are qualified throughout, as in AuthorizationTest: Jinaga.Test declares types
// whose names would otherwise shadow them.

/// <summary>
/// Only a user who owns the workspace of both endpoints may author a Link. Each formulation of
/// that rule joins to both endpoints' workspaces, and the split walks both in the head, so the
/// tail is never given the Link, which is not in the store while it is being authorized.
/// </summary>
public class LinkAuthorizationTest
{
    private static readonly User Alice = new("---ALICE---");
    private static readonly User Bob = new("---BOB---");
    private static readonly Model.Workspaces.Workspace AliceWorkspace = new(Alice, "alice");
    private static readonly Model.Workspaces.Workspace BobWorkspace = new(Bob, "bob");
    private static readonly Model.Workspaces.Item First = new(AliceWorkspace, "first");
    private static readonly Model.Workspaces.Item Second = new(AliceWorkspace, "second");
    private static readonly Model.Workspaces.Item Foreign = new(BobWorkspace, "foreign");

    // Formulation A of jinaga/jinaga.js#231: one Owner joined to both endpoints' workspaces.
    private static AuthorizationRules FormulationA(AuthorizationRules a) => a
        .Type<Model.Workspaces.Link>((link, facts) =>
            from owner in facts.OfType<Model.Workspaces.Owner>()
            where owner.workspace == link.item.workspace
            where owner.workspace == link.parent.workspace
            from user in facts.OfType<User>()
            where user == owner.user
            select user);

    // Formulation C: walk one endpoint's workspace first, then join the Owner to the other.
    private static AuthorizationRules FormulationC(AuthorizationRules a) => a
        .Type<Model.Workspaces.Link>((link, facts) =>
            from workspace in facts.OfType<Model.Workspaces.Workspace>()
            where workspace == link.item.workspace
            from owner in facts.OfType<Model.Workspaces.Owner>()
            where owner.workspace == workspace
            where owner.workspace == link.parent.workspace
            from user in facts.OfType<User>()
            where user == owner.user
            select user);

    // Formulation D: the second endpoint's workspace is tested inside a positive existential
    // condition. Walking it in the head is what keeps the Link out of the tail's givens.
    private static AuthorizationRules FormulationD(AuthorizationRules a) => a
        .Type<Model.Workspaces.Link>((link, facts) =>
            from owner in facts.OfType<Model.Workspaces.Owner>()
            where owner.workspace == link.item.workspace
            where facts.Any<Model.Workspaces.Owner>(other =>
                other.workspace == link.parent.workspace && other.user == owner.user)
            from user in facts.OfType<User>()
            where user == owner.user
            select user);

    public static TheoryData<string> Formulations => new() { "A", "C", "D" };

    [Theory]
    [MemberData(nameof(Formulations))]
    public async Task TheOwnerOfBothEndpointsWorkspaceMayLinkThem(string formulation)
    {
        var j = ClientFor(Alice, formulation);

        var link = await j.Fact(new Model.Workspaces.Link(Second, First));

        link.parent.Should().Be(First);
    }

    [Theory]
    [MemberData(nameof(Formulations))]
    public async Task ALinkIntoAWorkspaceTheUserDoesNotOwnIsRefused(string formulation)
    {
        var j = ClientFor(Alice, formulation);

        Func<Task> linking = async () => await j.Fact(new Model.Workspaces.Link(Second, Foreign));

        await linking.Should().ThrowAsync<AuthorizationException>();
    }

    [Fact]
    public void ARuleThatWalksTheGivensPredecessorsBeneathANegationIsRefusedWhereItIsWritten()
    {
        // Only an owner of the item's workspace may link it, unless the owner's workspace is
        // archived as the parent's. The walk to the parent's workspace sits beneath a negative
        // existential condition, where the split must not move it into the head: if the parent
        // role named several items, the tail would test their workspaces one at a time, and an
        // unarchived one would admit a link that an archived one should refuse. So the tail
        // reads the Link.
        Action building = () => JinagaTest.Create(options =>
        {
            options.User = Alice;
            options.Authorization = a => a
                .Type<Model.Workspaces.Link>((link, facts) =>
                    from owner in facts.OfType<Model.Workspaces.Owner>()
                    where owner.workspace == link.item.workspace
                    where !facts.Any<Model.Workspaces.Archive>(archive =>
                        archive.workspace == owner.workspace &&
                        archive.workspace == link.parent.workspace)
                    from user in facts.OfType<User>()
                    where user == owner.user
                    select user);
        });

        building.Should().Throw<InvalidOperationException>()
            .WithMessage("*reads 'link' from the store*inside a negative existential condition*");
    }

    private static JinagaClient ClientFor(User user, string formulation) => JinagaTest.Create(options =>
    {
        options.User = user;
        options.InitialState = ImmutableList.Create<object>(
            new Model.Workspaces.Owner(AliceWorkspace, Alice),
            new Model.Workspaces.Owner(BobWorkspace, Bob),
            First, Second, Foreign);
        options.Authorization = formulation switch
        {
            "A" => FormulationA,
            "C" => FormulationC,
            "D" => FormulationD,
            _ => throw new ArgumentOutOfRangeException(nameof(formulation))
        };
    });
}
