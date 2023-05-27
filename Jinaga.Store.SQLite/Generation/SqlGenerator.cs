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
            var allLabels = queryDescription.Inputs
                .Select(input => new SpecificationLabel(input.Label, input.FactIndex, input.Type))
                .Concat(queryDescription.Outputs
                    .Select(output => new SpecificationLabel(output.Label, output.FactIndex, output.Type)))
                .ToImmutableList();
            var columns = allLabels.Select(label =>
                $"f{label.Index}.hash as hash{label.Index}, " +
                $"f{label.Index}.fact_id as id{label.Index}, " +
                $"f{label.Index}.data as data{label.Index}")
                .Join(", ");
            var firstEdge = queryDescription.Edges.First();
            var predecessorInput = queryDescription.Inputs.Find(input => input.FactIndex == firstEdge.PredecessorFactIndex);
            var successorInput = queryDescription.Inputs.Find(input => input.FactIndex == firstEdge.SuccessorFactIndex);
            var firstFactIndex = predecessorInput != null ? predecessorInput.FactIndex : successorInput.FactIndex;
            var writtenFactIndexes = new HashSet<int> { firstFactIndex };
            var joins = GenerateJoins(queryDescription.Edges, writtenFactIndexes);
            var inputWhereClauses = queryDescription.Inputs
                .Select(input => $"f{input.FactIndex}.fact_type_id = ?{input.FactTypeParameter} AND f{input.FactIndex}.hash = ?{input.FactHashParameter}")
                .Join(" AND ");
            var existentialWhereClauses = queryDescription.ExistentialConditions
                .Select(existentialCondition => $" AND {(existentialCondition.Exists ? "EXISTS" : "NOT EXISTS")} ({GenerateExistentialWhereClause(existentialCondition, writtenFactIndexes)})")
                .Join("");
            var orderByClause = queryDescription.Outputs
                .Select(output => $"f{output.FactIndex}.fact_id ASC")
                .Join(", ");

            var sql = $"SELECT {columns} FROM fact f{firstFactIndex}{joins.Join("")} WHERE {inputWhereClauses}{existentialWhereClauses} ORDER BY {orderByClause}";
            return new SpecificationSqlQuery(sql, queryDescription.Parameters, allLabels);
        }

        private static string GenerateExistentialWhereClause(ExistentialConditionDescription existentialCondition, HashSet<int> outerFactIndexes)
        {
            var firstEdge = existentialCondition.Edges.First();
            var writtenFactIndexes = new HashSet<int>(outerFactIndexes);
            var firstJoin = ImmutableList<string>.Empty;
            var whereClause = ImmutableList<string>.Empty;
            if (writtenFactIndexes.Contains(firstEdge.PredecessorFactIndex))
            {
                if (writtenFactIndexes.Contains(firstEdge.SuccessorFactIndex))
                {
                    throw new NotImplementedException();
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

            var tailJoins = GenerateJoins(existentialCondition.Edges.Skip(1).ToImmutableList(), writtenFactIndexes);
            var joins = firstJoin.AddRange(tailJoins);
            var inputWhereClauses = existentialCondition.Inputs
                .Select(input => $" AND f{input.FactIndex}.fact_type_id = ?{input.FactTypeParameter} AND f{input.FactIndex}.hash = ?{input.FactHashParameter}")
                .Join("");
            var existentialWhereClauses = existentialCondition.ExistentialConditions
                .Select(condition => $" AND {(condition.Exists ? "EXISTS" : "NOT EXISTS")} ({GenerateExistentialWhereClause(condition, writtenFactIndexes)})")
                .Join("");
            return $"SELECT 1 FROM edge e{firstEdge.EdgeIndex}{joins.Join("")} WHERE {whereClause.Join(" AND ")}{inputWhereClauses}{existentialWhereClauses}";
        }

        private static ImmutableList<string> GenerateJoins(ImmutableList<EdgeDescription> edges, HashSet<int> writtenFactIndexes)
        {
            var joins = ImmutableList<string>.Empty;
            foreach (var edge in edges)
            {
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
    }
}
