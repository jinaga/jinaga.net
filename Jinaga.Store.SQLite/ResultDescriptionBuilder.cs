using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Projections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Store.SQLite
{
    internal class FactDescription
    {
        public FactDescription(string type, int factIndex)
        {
            Type = type;
            FactIndex = factIndex;
        }

        public string Type { get; }
        public int FactIndex { get; }
    }
    internal class QueryDescription
    {
        public static readonly QueryDescription Empty;

        private QueryDescription()
        {
        }

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
        private class Context
        {
            public static readonly Context Empty = new Context(
                QueryDescription.Empty,
                ImmutableDictionary<string, FactDescription>.Empty,
                ImmutableList<int>.Empty);

            public QueryDescription QueryDescription { get; }
            public ImmutableDictionary<string, FactDescription> FactByLabel { get; }
            public ImmutableList<int> Path { get; }

            public Context(QueryDescription queryDescription, ImmutableDictionary<string, FactDescription> factByLabel, ImmutableList<int> path)
            {
                QueryDescription = queryDescription;
                FactByLabel = factByLabel;
                Path = path;
            }
        }

        private ImmutableDictionary<string, int> factTypes;
        private ImmutableDictionary<int, ImmutableDictionary<string, int>> roleMap;

        public ResultDescriptionBuilder(ImmutableDictionary<string, int> factTypes, ImmutableDictionary<int, ImmutableDictionary<string, int>> roleMap)
        {
            this.factTypes = factTypes;
            this.roleMap = roleMap;
        }

        public ResultDescription Build(ImmutableList<FactReference> startReferences, Specification specification)
        {
            // Verify that the number of start references matches the number of given facts.
            if (startReferences.Count != specification.Given.Count)
            {
                throw new ArgumentException($"The number of start facts ({startReferences.Count}) does not match the number of inputs ({specification.Given.Count}).");
            }
            // Verify that the start reference types match the given fact types.
            for (var i = 0; i < startReferences.Count; i++)
            {
                var startReference = startReferences[i];
                var given = specification.Given[i];
                if (startReference.Type != given.Type)
                {
                    throw new ArgumentException($"The start fact type ({startReference.Type}) does not match the input type ({given.Type}).");
                }
            }

            var context = Context.Empty;
            return CreateResultDescription(context, specification.Given, startReferences, specification.Matches, specification.Projection);
        }

        private ResultDescription CreateResultDescription(Context context, ImmutableList<Label> given, ImmutableList<FactReference> startReferences, ImmutableList<Match> matches, Projection projection)
        {
            var givenTuple = given
                .Select((label, index) =>
                    KeyValuePair.Create(label.Name, startReferences[index]))
                .ToImmutableDictionary();
            context = AddEdges(context, given, startReferences, matches);
            throw new NotImplementedException();
        }

        private Context AddEdges(Context context, ImmutableList<Label> given, ImmutableList<FactReference> startReferences, ImmutableList<Match> matches)
        {
            throw new NotImplementedException();
        }
    }
}