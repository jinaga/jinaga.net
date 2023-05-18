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
            var predecessorFact = queryDescription.Facts.Find(fact => fact.FactIndex == firstEdge.PredecessorFactIndex);
            var successorFact = queryDescription.Facts.Find(fact => fact.FactIndex == firstEdge.SuccessorFactIndex);
            var firstFactIndex = predecessorFact != null ? predecessorFact.FactIndex : successorFact.FactIndex;
            var writtenFactIndexes = new HashSet<int> { firstFactIndex };
            var joins = GenerateJoins(queryDescription.Edges, writtenFactIndexes);
            var inputWhereClauses = queryDescription.Inputs
                .Select(input => $"f{input.FactIndex}.fact_type_id = ${input.FactTypeParameter} AND f{input.FactIndex}.hash = ${input.FactHashParameter}")
                .Join(" AND ");
            var orderByClause = queryDescription.Outputs
                .Select(output => $"f{output.FactIndex}.fact_id ASC")
                .Join(", ");

            var sql = $"SELECT {columns} FROM fact f{firstFactIndex}{joins.Join("")} WHERE {inputWhereClauses} ORDER BY {orderByClause}";
            return new SpecificationSqlQuery(sql, queryDescription.Parameters, allLabels);
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
                            $" AND e{edge.EdgeIndex}.role_id = ${edge.RoleParameter}"
                        );
                    }
                    else
                    {
                        joins = joins.Add(
                            $" JOIN edge e{edge.EdgeIndex}" +
                            $" ON e{edge.EdgeIndex}.predecessor_fact_id = f{edge.PredecessorFactIndex}.fact_id" +
                            $" AND e{edge.EdgeIndex}.role_id = ${edge.RoleParameter}"
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
                        $" AND e{edge.EdgeIndex}.role_id = ${edge.RoleParameter}"
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
