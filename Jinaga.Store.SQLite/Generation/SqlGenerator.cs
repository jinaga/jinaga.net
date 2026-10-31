using Jinaga.Store.SQLite.Description;
using Jinaga.Visualizers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Store.SQLite.Generation
{
    internal static class SqlGenerator
    {
        public static SqlQueryTree CreateSqlQueryTree(ResultDescription description, int parentFactIdLength = 0)
        {
            var sqlQuery = GenerateResultSqlQuery(description.QueryDescription);
            var childQueries = description.ChildResultDescriptions
                .Select(child => KeyValuePair.Create(
                    child.Key,
                    CreateSqlQueryTree(child.Value, description.QueryDescription.OutputLength())))
                .ToImmutableDictionary();
            return new SqlQueryTree(sqlQuery, parentFactIdLength, childQueries);
        }

        private static SpecificationSqlQuery GenerateResultSqlQuery(QueryDescription queryDescription)
        {
            if (!queryDescription.IsSatisfiable())
            {
                return SpecificationSqlQuery.Empty;
            }
            var allLabels = queryDescription.Inputs
                .Select(input => new SpecificationLabel(input.Label, input.FactIndex, input.Type))
                .Concat(queryDescription.Outputs
                    .Select(output => new SpecificationLabel(output.Label, output.FactIndex, output.Type)))
                .ToImmutableList();
            // One column per fact, not per label. Two labels name one fact when a
            // specification pins an unknown to a given: the unknown is a label of its own and
            // resolves to the given's fact, so emitting its columns twice would write the
            // same alias twice.
            var columns = allLabels
                .GroupBy(label => label.Index)
                .Select(group => group.Key)
                .Select(index =>
                    $"f{index}.hash as hash{index}, " +
                    $"f{index}.fact_id as id{index}, " +
                    $"f{index}.data as data{index}")
                .Join(", ");
            var firstFactIndex = FirstFactIndex(queryDescription);
            var writtenFactIndexes = new HashSet<int> { firstFactIndex };
            var joins = GenerateJoins(queryDescription.Edges, writtenFactIndexes, queryDescription.Inputs);
            joins = joins.AddRange(AnchorUnjoinedInputs(queryDescription.Inputs, writtenFactIndexes));
            var inputWhereClauses = queryDescription.Inputs
                .Select(input => $"f{input.FactIndex}.fact_type_id = ?{input.FactTypeParameter} AND f{input.FactIndex}.hash = ?{input.FactHashParameter}")
                .Join(" AND ");
            var factEqualityWhereClauses = queryDescription.FactEqualities
                .Select(factEquality => $" AND {FactEqualityClause(factEquality)}")
                .Join("");
            var existentialWhereClauses = queryDescription.ExistentialConditions
                .Select(existentialCondition => $" AND {(existentialCondition.Exists ? "EXISTS" : "NOT EXISTS")} ({GenerateExistentialWhereClause(existentialCondition, writtenFactIndexes)})")
                .Join("");
            var orderByClause = queryDescription.Outputs
                .Select(output => $"f{output.FactIndex}.fact_id ASC")
                .Join(", ");

            var sql = $"SELECT {columns} FROM fact f{firstFactIndex}{joins.Join("")} WHERE {inputWhereClauses}{factEqualityWhereClauses}{existentialWhereClauses} ORDER BY {orderByClause}";
            return new SpecificationSqlQuery(sql, queryDescription.Parameters, allLabels);
        }

        /// <summary>
        /// The fact the query hangs its from clause on: a given at one end of the first edge,
        /// or, where no edge names one, the first given.
        /// </summary>
        /// <remarks>
        /// Every given carries a type and a hash, so a given is the one fact the query can
        /// name outright. A specification can state a match as an equality with a given and
        /// no step between facts, which contributes no edge at all, so the first edge is not
        /// always there to choose from and need not name a given when it is.
        /// <see cref="GenerateJoins"/> reaches the rest from whatever this returns.
        /// </remarks>
        private static int FirstFactIndex(QueryDescription queryDescription)
        {
            var firstEdge = queryDescription.Edges.FirstOrDefault();
            if (firstEdge != null)
            {
                var predecessorInput = queryDescription.Inputs.Find(
                    input => input.FactIndex == firstEdge.PredecessorFactIndex);
                if (predecessorInput != null)
                {
                    return predecessorInput.FactIndex;
                }
                var successorInput = queryDescription.Inputs.Find(
                    input => input.FactIndex == firstEdge.SuccessorFactIndex);
                if (successorInput != null)
                {
                    return successorInput.FactIndex;
                }
            }
            // A satisfiable query has at least one input.
            return queryDescription.Inputs.First().FactIndex;
        }

        /// <summary>
        /// Joins each given that no edge reached, so the query supplies every column it
        /// selects and filters.
        /// </summary>
        /// <remarks>
        /// A given's type and hash go in the where clause and its columns in the select list
        /// whether or not an edge joins it, and a given named by an equality alone is joined
        /// by none. The hash pins it to one fact, so joining it on its own constrains
        /// nothing further.
        /// </remarks>
        private static ImmutableList<string> AnchorUnjoinedInputs(
            ImmutableList<InputDescription> inputs, HashSet<int> writtenFactIndexes)
        {
            var joins = ImmutableList<string>.Empty;
            foreach (var input in inputs)
            {
                if (writtenFactIndexes.Add(input.FactIndex))
                {
                    joins = joins.Add($" JOIN fact f{input.FactIndex} ON 1 = 1");
                }
            }
            return joins;
        }

        private static string GenerateExistentialWhereClause(ExistentialConditionDescription existentialCondition, HashSet<int> outerFactIndexes)
        {
            if (existentialCondition.Edges.IsEmpty)
            {
                return GenerateEdgelessExistentialWhereClause(existentialCondition, outerFactIndexes);
            }
            var firstEdge = existentialCondition.Edges.First();
            var writtenFactIndexes = new HashSet<int>(outerFactIndexes);
            var firstJoin = ImmutableList<string>.Empty;
            var whereClause = ImmutableList<string>.Empty;
            if (writtenFactIndexes.Contains(firstEdge.PredecessorFactIndex))
            {
                if (writtenFactIndexes.Contains(firstEdge.SuccessorFactIndex))
                {
                    whereClause = whereClause.Add(
                        $"e{firstEdge.EdgeIndex}.predecessor_fact_id = f{firstEdge.PredecessorFactIndex}.fact_id" +
                        $" AND e{firstEdge.EdgeIndex}.successor_fact_id = f{firstEdge.SuccessorFactIndex}.fact_id" +
                        $" AND e{firstEdge.EdgeIndex}.role_id = ?{firstEdge.RoleParameter}"
                    );
                }
                else
                {
                    whereClause = whereClause.Add(
                        $"e{firstEdge.EdgeIndex}.predecessor_fact_id = f{firstEdge.PredecessorFactIndex}.fact_id" +
                        $" AND e{firstEdge.EdgeIndex}.role_id = ?{firstEdge.RoleParameter}"
                    );
                    firstJoin = firstJoin.Add(
                        $" JOIN fact f{firstEdge.SuccessorFactIndex}" +
                        $" ON f{firstEdge.SuccessorFactIndex}.fact_id = e{firstEdge.EdgeIndex}.successor_fact_id"
                    );
                    writtenFactIndexes.Add(firstEdge.SuccessorFactIndex);
                }
            }
            else if (writtenFactIndexes.Contains(firstEdge.SuccessorFactIndex))
            {
                whereClause = whereClause.Add(
                    $"e{firstEdge.EdgeIndex}.successor_fact_id = f{firstEdge.SuccessorFactIndex}.fact_id" +
                    $" AND e{firstEdge.EdgeIndex}.role_id = ?{firstEdge.RoleParameter}"
                );
                firstJoin = firstJoin.Add(
                    $" JOIN fact f{firstEdge.PredecessorFactIndex}" +
                    $" ON f{firstEdge.PredecessorFactIndex}.fact_id = e{firstEdge.EdgeIndex}.predecessor_fact_id"
                );
                writtenFactIndexes.Add(firstEdge.PredecessorFactIndex);
            }
            else
            {
                throw new ArgumentException("Neither predecessor nor successor fact has been written");
            }

            var tailJoins = GenerateJoins(
                existentialCondition.Edges.Skip(1).ToImmutableList(),
                writtenFactIndexes,
                existentialCondition.Inputs);
            var joins = firstJoin
                .AddRange(tailJoins)
                .AddRange(AnchorUnjoinedInputs(existentialCondition.Inputs, writtenFactIndexes));
            var inputWhereClauses = existentialCondition.Inputs
                .Select(input => $" AND f{input.FactIndex}.fact_type_id = ?{input.FactTypeParameter} AND f{input.FactIndex}.hash = ?{input.FactHashParameter}")
                .Join("");
            var factEqualityWhereClauses = existentialCondition.FactEqualities
                .Select(factEquality => $" AND {FactEqualityClause(factEquality)}")
                .Join("");
            var existentialWhereClauses = existentialCondition.ExistentialConditions
                .Select(condition => $" AND {(condition.Exists ? "EXISTS" : "NOT EXISTS")} ({GenerateExistentialWhereClause(condition, writtenFactIndexes)})")
                .Join("");
            return $"SELECT 1 FROM edge e{firstEdge.EdgeIndex}{joins.Join("")} WHERE {whereClause.Join(" AND ")}{inputWhereClauses}{factEqualityWhereClauses}{existentialWhereClauses}";
        }

        /// <summary>
        /// Joins the edges, reaching each from a fact already written and anchoring a
        /// component that none of them reaches on a given it names.
        /// </summary>
        /// <remarks>
        /// A specification's matches need not form one connected component. An inverse whose
        /// parts meet only at the givens is the shape that proves it: jinaga-spec derives one
        /// for a specification whose given carries a condition, so refusing a disconnected
        /// specification would refuse a watch the model admits. Each component reaches a
        /// given, and a given is pinned to one fact by its hash, so anchoring a component
        /// there joins the components on the facts they share rather than multiplying rows.
        /// </remarks>
        private static ImmutableList<string> GenerateJoins(
            ImmutableList<EdgeDescription> edges,
            HashSet<int> writtenFactIndexes,
            ImmutableList<InputDescription> inputs)
        {
            var joins = ImmutableList<string>.Empty;
            var remainingEdges = edges;
            while (remainingEdges.Count > 0)
            {
                // Find an edge for which either a predecessor or successor fact has been written.
                var edgeIndex = remainingEdges.FindIndex(edge =>
                    writtenFactIndexes.Contains(edge.PredecessorFactIndex) ||
                    writtenFactIndexes.Contains(edge.SuccessorFactIndex)
                );

                // No such edge means what remains is a component of its own. Anchor it on a
                // given it names and carry on from there.
                if (edgeIndex < 0)
                {
                    var anchorFactIndex = AnchorFactIndex(remainingEdges, inputs);

                    // No given in it either, so nothing names a fact of this component and
                    // the graph is not connected.
                    if (anchorFactIndex == null)
                    {
                        throw new ArgumentException("The specification is not connected");
                    }
                    joins = joins.Add($" JOIN fact f{anchorFactIndex.Value} ON 1 = 1");
                    writtenFactIndexes.Add(anchorFactIndex.Value);
                    continue;
                }

                // Write the edge.
                var edge = remainingEdges[edgeIndex];
                remainingEdges = remainingEdges.RemoveAt(edgeIndex);

                if (writtenFactIndexes.Contains(edge.PredecessorFactIndex))
                {
                    if (writtenFactIndexes.Contains(edge.SuccessorFactIndex))
                    {
                        joins = joins.Add(
                            $" JOIN edge e{edge.EdgeIndex}" +
                            $" ON e{edge.EdgeIndex}.predecessor_fact_id = f{edge.PredecessorFactIndex}.fact_id" +
                            $" AND e{edge.EdgeIndex}.successor_fact_id = f{edge.SuccessorFactIndex}.fact_id" +
                            $" AND e{edge.EdgeIndex}.role_id = ?{edge.RoleParameter}"
                        );
                    }
                    else
                    {
                        joins = joins.Add(
                            $" JOIN edge e{edge.EdgeIndex}" +
                            $" ON e{edge.EdgeIndex}.predecessor_fact_id = f{edge.PredecessorFactIndex}.fact_id" +
                            $" AND e{edge.EdgeIndex}.role_id = ?{edge.RoleParameter}"
                        );
                        joins = joins.Add(
                            $" JOIN fact f{edge.SuccessorFactIndex}" +
                            $" ON f{edge.SuccessorFactIndex}.fact_id = e{edge.EdgeIndex}.successor_fact_id"
                        );
                        writtenFactIndexes.Add(edge.SuccessorFactIndex);
                    }
                }
                else if (writtenFactIndexes.Contains(edge.SuccessorFactIndex))
                {
                    joins = joins.Add(
                        $" JOIN edge e{edge.EdgeIndex}" +
                        $" ON e{edge.EdgeIndex}.successor_fact_id = f{edge.SuccessorFactIndex}.fact_id" +
                        $" AND e{edge.EdgeIndex}.role_id = ?{edge.RoleParameter}"
                    );
                    joins = joins.Add(
                        $" JOIN fact f{edge.PredecessorFactIndex}" +
                        $" ON f{edge.PredecessorFactIndex}.fact_id = e{edge.EdgeIndex}.predecessor_fact_id"
                    );
                    writtenFactIndexes.Add(edge.PredecessorFactIndex);
                }
                else
                {
                    throw new ArgumentException("Neither predecessor nor successor fact has been written");
                }
            }
            return joins;
        }

        /// <summary>
        /// The fact index of a given named by one of <paramref name="remainingEdges"/>, or
        /// null where none of them names one.
        /// </summary>
        private static int? AnchorFactIndex(
            ImmutableList<EdgeDescription> remainingEdges, ImmutableList<InputDescription> inputs)
        {
            var factIndexes = remainingEdges
                .SelectMany(edge => new[] { edge.PredecessorFactIndex, edge.SuccessorFactIndex })
                .ToImmutableHashSet();
            return inputs.Find(input => factIndexes.Contains(input.FactIndex))?.FactIndex;
        }

        /// <summary>
        /// The constraint that two fact indexes name one fact, as a where clause.
        /// </summary>
        /// <remarks>
        /// Both ends are facts the query already joins, so the clause relates two of its own
        /// aliases and joins nothing further. Inside an existential condition one or both ends
        /// can belong to the outer query, which correlates the subquery on that fact.
        /// </remarks>
        private static string FactEqualityClause(FactEqualityDescription factEquality) =>
            $"f{factEquality.LeftFactIndex}.fact_id = f{factEquality.RightFactIndex}.fact_id";

        /// <summary>
        /// A condition that contributes no edge: every match in it names a fact that some
        /// label already binds, so there is no step between facts to walk.
        /// </summary>
        /// <remarks>
        /// What such a condition still says is the type and hash of each given it names, and
        /// whatever its own nested conditions require. A match whose only condition is an
        /// equality with no roles on either side reaches this, and the inverses jinaga-spec
        /// derives contain one. Asking the edges for their first threw instead, so the read
        /// failed rather than answering.
        ///
        /// With nothing left to constrain, the condition holds of the fact it names, which
        /// exists: the select carries no from clause, and the condition is true.
        /// </remarks>
        private static string GenerateEdgelessExistentialWhereClause(
            ExistentialConditionDescription existentialCondition, HashSet<int> outerFactIndexes)
        {
            var writtenFactIndexes = new HashSet<int>(outerFactIndexes);
            var fromClauses = ImmutableList<string>.Empty;
            foreach (var input in existentialCondition.Inputs)
            {
                if (writtenFactIndexes.Add(input.FactIndex))
                {
                    fromClauses = fromClauses.Add($"fact f{input.FactIndex}");
                }
            }
            var whereClauses = existentialCondition.Inputs
                .Select(input =>
                    $"f{input.FactIndex}.fact_type_id = ?{input.FactTypeParameter}" +
                    $" AND f{input.FactIndex}.hash = ?{input.FactHashParameter}")
                .Concat(existentialCondition.FactEqualities
                    .Select(FactEqualityClause))
                .Concat(existentialCondition.ExistentialConditions
                    .Select(condition =>
                        $"{(condition.Exists ? "EXISTS" : "NOT EXISTS")}" +
                        $" ({GenerateExistentialWhereClause(condition, writtenFactIndexes)})"))
                .ToImmutableList();
            var fromClause = fromClauses.IsEmpty ? "" : $" FROM {fromClauses.Join(", ")}";
            var whereClause = whereClauses.IsEmpty ? "" : $" WHERE {whereClauses.Join(" AND ")}";
            return $"SELECT 1{fromClause}{whereClause}";
        }
    }
}
