using System.Collections.Immutable;
using System.Linq;
using Jinaga.Projections;

namespace Jinaga.Test;

/// <summary>
/// A skeleton spread out into its fields, so that two skeletons can be compared without a
/// <c>Skeleton.Equals</c>.
///
/// <para>
/// <see cref="Skeleton"/> deliberately overrides neither <c>Equals</c> nor
/// <c>GetHashCode</c>. The comparisons a replicator makes are the distribution engine's
/// (<c>skeletonsEqual</c> over sets, <c>skeletonContains</c>, and the permutation of the
/// inputs), and an <c>Equals</c> that matched none of them would invite a test, or a caller,
/// to compare skeletons by a rule the replicator does not use. So a test states which fields
/// it is comparing, and this is how it says so.
/// </para>
///
/// <para>
/// Every field of every element participates, which is what makes the comparison total: a
/// skeleton that differs anywhere differs in one of the lists below. Each list is flat and
/// holds only scalars, because a tuple compares its fields with the default comparer and
/// <c>ImmutableList</c> has no structural equality of its own; a list nested inside a tuple
/// would be compared by reference and would pass on any two skeletons.
/// </para>
///
/// <para>
/// The not-exists conditions nest, so they are stated as two lists: the path of positions
/// that reaches each condition, and every edge of every condition attributed to the path it
/// sits in. Together they fix the nesting exactly. Two skeletons whose conditions nest
/// differently hold different paths even when their edges agree, and a condition that holds
/// no edges still appears among the paths, so an added or removed empty condition is a
/// difference like any other.
/// </para>
/// </summary>
internal static class SkeletonFields
{
    internal static ImmutableList<(string FactType, int FactIndex)> Facts(Skeleton skeleton) =>
        skeleton.Facts.Select(fact => (fact.FactType, fact.FactIndex)).ToImmutableList();

    internal static ImmutableList<(int FactIndex, int InputIndex)> Inputs(Skeleton skeleton) =>
        skeleton.Inputs.Select(input => (input.FactIndex, input.InputIndex)).ToImmutableList();

    /// <summary>The edges outside every not-exists condition.</summary>
    internal static ImmutableList<(int EdgeIndex, int Predecessor, int Successor, string RoleName)>
        Edges(Skeleton skeleton) =>
        skeleton.Edges
            .Select(edge =>
                (edge.EdgeIndex, edge.PredecessorFactIndex, edge.SuccessorFactIndex, edge.RoleName))
            .ToImmutableList();

    internal static ImmutableList<int> Outputs(Skeleton skeleton) =>
        skeleton.Outputs.Select(output => output.FactIndex).ToImmutableList();

    /// <summary>
    /// The path of every not-exists condition in the skeleton, in the order the walk opened
    /// them. A path is the positions that reach the condition, outermost first, so a nested
    /// condition's path extends the path of the condition that holds it.
    /// </summary>
    internal static ImmutableList<string> ConditionPaths(Skeleton skeleton) =>
        Paths(skeleton.NotExistsConditions, "");

    /// <summary>Every edge of every not-exists condition, under the path of the condition it sits in.</summary>
    internal static ImmutableList<(string Path, int EdgeIndex, int Predecessor, int Successor, string RoleName)>
        ConditionEdges(Skeleton skeleton) =>
        ConditionEdges(skeleton.NotExistsConditions, "");

    private static ImmutableList<string> Paths(
        ImmutableList<NotExistsConditionDescription> conditions, string prefix) =>
        conditions
            .SelectMany((condition, index) =>
                Paths(condition.NotExistsConditions, prefix + index + ".")
                    .Insert(0, prefix + index))
            .ToImmutableList();

    private static ImmutableList<(string Path, int EdgeIndex, int Predecessor, int Successor, string RoleName)>
        ConditionEdges(ImmutableList<NotExistsConditionDescription> conditions, string prefix) =>
        conditions
            .SelectMany((condition, index) =>
            {
                var path = prefix + index;
                return condition.Edges
                    .Select(edge => (path, edge.EdgeIndex, edge.PredecessorFactIndex,
                        edge.SuccessorFactIndex, edge.RoleName))
                    .Concat(ConditionEdges(condition.NotExistsConditions, path + "."));
            })
            .ToImmutableList();
}
