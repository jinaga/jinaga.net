using System;
using System.Collections.Immutable;
using Jinaga.Pipelines;
using Jinaga.Projections;

namespace Jinaga.Test.Specifications;

/// <summary>
/// What a skeleton distinguishes, and what it does not.
///
/// <para>
/// A replicator serves a feed when its skeleton matches the skeleton of a feed of one of its
/// distribution rules, so what the skeleton leaves out is what two specifications may differ
/// in and still be the same rule. It leaves out the labels: a rule written over
/// <c>event</c> covers a query written over <c>e</c>. It keeps the edges: a rule over an
/// event's deletions does not cover a query over its approvals.
/// </para>
///
/// <para>
/// The skeletons of the reference's own feeds are stated by the pinned vectors, which
/// <see cref="Conformance.SkeletonVectorTest"/> reads. These two specifications are built
/// directly, as <see cref="FeedBuilderTest"/> builds its own, because what is under test is
/// the relation between two skeletons rather than any one skeleton's value.
/// </para>
/// </summary>
public class SkeletonTest
{
    private static readonly Role TenantRole = new("tenant", "Tenant");
    private static readonly Role EventRole = new("event", "Event");
    private static readonly Role ApprovalRole = new("approval", "Approval");

    [Fact]
    public void RelabellingLeavesTheSkeletonUnchanged()
    {
        // (t: Tenant) { e: Event [ e->tenant = t ] } and the same over 'tenant' and 'event'.
        var terse = Spec("t", Match("e", "Event", Successor("t", TenantRole)));
        var spelledOut = Spec("tenant", Match("event", "Event", Successor("tenant", TenantRole)));

        Skeleton.Of(terse).Should().Be(Skeleton.Of(spelledOut));
    }

    [Fact]
    public void ARelabelledSkeletonIsEqualThroughEveryNestedCondition()
    {
        // The labels a negative condition declares are left out too, not only the ones the
        // outer matches declare. A skeleton records a condition's edges and no names at all.
        var terse = Spec("t",
            Match("e", "Event",
                new[] { Successor("t", TenantRole) },
                Exists(false, Match("a", "Approval", Successor("e", EventRole)))));
        var spelledOut = Spec("tenant",
            Match("event", "Event",
                new[] { Successor("tenant", TenantRole) },
                Exists(false, Match("approval", "Approval", Successor("event", EventRole)))));

        Skeleton.Of(terse).Should().Be(Skeleton.Of(spelledOut));
    }

    [Fact]
    public void AnAddedEdgeChangesTheSkeleton()
    {
        // One walks to the event's approvals; the other to the approvals of its approvals.
        var oneEdge = Spec("t",
            Match("e", "Event", Successor("t", TenantRole)),
            Match("a", "Approval", Successor("e", EventRole)));
        var twoEdges = Spec("t",
            Match("e", "Event", Successor("t", TenantRole)),
            Match("a", "Approval", Successor("e", EventRole)),
            Match("a2", "Approval", Successor("a", ApprovalRole)));

        Skeleton.Of(oneEdge).Should().NotBe(Skeleton.Of(twoEdges));
    }

    [Fact]
    public void AnEdgeOnADifferentRoleChangesTheSkeleton()
    {
        // The same shape, the same number of edges, and a different role at the last step.
        // Nothing about the skeleton's size separates these two: only the role name does.
        var byEvent = Spec("t",
            Match("e", "Event", Successor("t", TenantRole)),
            Match("a", "Approval", Successor("e", EventRole)));
        var byRevocation = Spec("t",
            Match("e", "Event", Successor("t", TenantRole)),
            Match("a", "Approval", Successor("e", new Role("revoked", "Event"))));

        Skeleton.Of(byEvent).Should().NotBe(Skeleton.Of(byRevocation));
    }

    /* ---------------------------------- Building them ---------------------------------- */

    // (<given>: Tenant) { matches } => {}
    private static Specification Spec(string given, params Match[] matches) =>
        new(
            ImmutableList.Create(new SpecificationGiven(
                new Label(given, "Tenant"), ImmutableList<ExistentialCondition>.Empty)),
            matches.ToImmutableList(),
            new CompoundProjection(ImmutableDictionary<string, Projection>.Empty, typeof(object)));

    private static Match Match(string name, string type, params PathCondition[] paths) =>
        Match(name, type, paths, Array.Empty<ExistentialCondition>());

    private static Match Match(
        string name, string type, PathCondition[] paths, params ExistentialCondition[] existentials) =>
        new(new Label(name, type), paths.ToImmutableList(), existentials.ToImmutableList());

    /// <summary>A walk from <paramref name="labelRight"/> to the unknown's successors.</summary>
    private static PathCondition Successor(string labelRight, params Role[] rolesLeft) =>
        new(rolesLeft.ToImmutableList(), labelRight, ImmutableList<Role>.Empty);

    private static ExistentialCondition Exists(bool exists, params Match[] matches) =>
        new(exists, matches.ToImmutableList());
}
