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
///
/// <para>
/// Two skeletons are compared field by field, through <see cref="SkeletonFields"/>, because
/// <c>Skeleton</c> has no <c>Equals</c>: the comparisons that decide distribution are the
/// engine's, and a skeleton that answered one of them would invite a caller to use it for
/// the others. The tests below that separate two skeletons say which field separates them,
/// which is a stronger claim than inequality: an added edge and an edge on another role are
/// both differences in the edges, and the second one is a difference in nothing else.
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

        ShouldHaveTheSameFields(Skeleton.Of(terse), Skeleton.Of(spelledOut));
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

        ShouldHaveTheSameFields(Skeleton.Of(terse), Skeleton.Of(spelledOut));
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

        SkeletonFields.Edges(Skeleton.Of(oneEdge)).Should()
            .NotEqual(SkeletonFields.Edges(Skeleton.Of(twoEdges)));
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

        var byEventSkeleton = Skeleton.Of(byEvent);
        var byRevocationSkeleton = Skeleton.Of(byRevocation);

        SkeletonFields.Edges(byEventSkeleton).Should()
            .NotEqual(SkeletonFields.Edges(byRevocationSkeleton));
        // Nothing else about the two skeletons differs, so the role name is carrying the
        // whole distinction rather than some incidental change of shape.
        SkeletonFields.Facts(byEventSkeleton).Should()
            .Equal(SkeletonFields.Facts(byRevocationSkeleton));
        SkeletonFields.Inputs(byEventSkeleton).Should()
            .Equal(SkeletonFields.Inputs(byRevocationSkeleton));
        SkeletonFields.Outputs(byEventSkeleton).Should()
            .Equal(SkeletonFields.Outputs(byRevocationSkeleton));
    }

    /// <summary>
    /// Every field of one skeleton against every field of the other: the facts, the inputs,
    /// the top-level edges, the nesting of the not-exists conditions, the edges inside them,
    /// and the outputs. A skeleton that differs anywhere differs in one of these.
    /// </summary>
    private static void ShouldHaveTheSameFields(Skeleton left, Skeleton right)
    {
        SkeletonFields.Facts(left).Should().Equal(SkeletonFields.Facts(right));
        SkeletonFields.Inputs(left).Should().Equal(SkeletonFields.Inputs(right));
        SkeletonFields.Edges(left).Should().Equal(SkeletonFields.Edges(right));
        SkeletonFields.ConditionPaths(left).Should().Equal(SkeletonFields.ConditionPaths(right));
        SkeletonFields.ConditionEdges(left).Should().Equal(SkeletonFields.ConditionEdges(right));
        SkeletonFields.Outputs(left).Should().Equal(SkeletonFields.Outputs(right));
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
