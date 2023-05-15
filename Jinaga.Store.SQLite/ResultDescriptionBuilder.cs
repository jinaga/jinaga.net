using Jinaga.Facts;
using Jinaga.Projections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Store.SQLite
{
    internal class QueryDescription
    {
        public SpecificationSqlQuery GenerateResultSqlQuery()
        {
            throw new NotImplementedException();
        }

        public bool IsSatisfiable()
        {
            throw new NotImplementedException();
        }

        public int OutputLength()
        {
            throw new NotImplementedException();
        }
    }

    internal class ResultDescription
    {
        public QueryDescription QueryDescription { get; set; }
        public ImmutableDictionary<string, ResultDescription> ChildResultDescriptions { get; set; }

        public SqlQueryTree CreateSqlQueryTree(int parentFactIdLength)
        {
            SpecificationSqlQuery sqlQuery = QueryDescription.GenerateResultSqlQuery();
            var childQueries = ChildResultDescriptions
                .Select(child => KeyValuePair.Create(
                    child.Key,
                    child.Value.CreateSqlQueryTree(QueryDescription.OutputLength())))
                .ToImmutableDictionary();
            return new SqlQueryTree(sqlQuery, parentFactIdLength, childQueries);
        }
    }

    internal class ResultDescriptionBuilder
    {
        private ImmutableDictionary<string, int> factTypes;
        private ImmutableDictionary<int, ImmutableDictionary<string, int>> roleMap;

        public ResultDescriptionBuilder(ImmutableDictionary<string, int> factTypes, ImmutableDictionary<int, ImmutableDictionary<string, int>> roleMap)
        {
            this.factTypes = factTypes;
            this.roleMap = roleMap;
        }

        public ResultDescription Build(ImmutableList<FactReference> startReferences, Specification specification)
        {
            throw new NotImplementedException();
        }
    }
}