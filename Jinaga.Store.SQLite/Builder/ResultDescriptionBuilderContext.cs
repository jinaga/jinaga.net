using Jinaga.Pipelines;
using Jinaga.Store.SQLite.Description;
using System;
using System.Collections.Immutable;

namespace Jinaga.Store.SQLite.Builder
{
    internal class ResultDescriptionBuilderContext
    {
        public static readonly ResultDescriptionBuilderContext Empty = new ResultDescriptionBuilderContext(
            QueryDescription.Empty,
            ImmutableDictionary<string, FactDescription>.Empty,
            ImmutableList<int>.Empty);

        public QueryDescription QueryDescription { get; }
        public ImmutableDictionary<string, FactDescription> KnownFacts { get; }
        public ImmutableList<int> Path { get; }

        public ResultDescriptionBuilderContext(QueryDescription queryDescription, ImmutableDictionary<string, FactDescription> factByLabel, ImmutableList<int> path)
        {
            QueryDescription = queryDescription;
            KnownFacts = factByLabel;
            Path = path;
        }

        public ResultDescriptionBuilderContext WithExistentialCondition(bool exists)
        {
            throw new NotImplementedException();
        }

        public ResultDescriptionBuilderContext WithQueryDescription(QueryDescription queryDescription)
        {
            throw new NotImplementedException();
        }

        public ResultDescriptionBuilderContext WithInputParameter(Label label, int factTypeId, string hash)
        {
            int factTypeParameter = QueryDescription.Parameters.Count + 1;
            int factHashParameter = QueryDescription.Parameters.Count + 2;
            int factIndex = QueryDescription.Facts.Count + 1;
            var factDescription = new FactDescription(label.Type, factIndex);
            var facts = QueryDescription.Facts.Add(factDescription);
            var input = new InputDescription(label.Name, label.Type, factIndex, factTypeParameter, factHashParameter);
            var parameters = QueryDescription.Parameters.Add(factTypeId).Add(hash);
            if (Path.Count == 0)
            {
                var inputs = QueryDescription.Inputs.Add(input);
                var queryDescription = QueryDescription.WithInputs(inputs).WithParameters(parameters).WithFacts(facts);
                return new ResultDescriptionBuilderContext(queryDescription, KnownFacts.Add(label.Name, factDescription), Path);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public (ResultDescriptionBuilderContext context, int factIndex) WithFact(string type)
        {
            int factIndex = QueryDescription.Facts.Count + 1;
            var fact = new FactDescription(type, factIndex);
            var queryDescription = QueryDescription.WithFacts(QueryDescription.Facts.Add(fact));
            var context = new ResultDescriptionBuilderContext(queryDescription, KnownFacts, Path);
            return (context, factIndex);
        }

        public ResultDescriptionBuilderContext WithEdge(int predecessorFactIndex, int successorFactIndex, int roleId)
        {
            var roleParameter = QueryDescription.Parameters.Count + 1;
            var parameters = QueryDescription.Parameters.Add(roleId);
            var queryDescription = QueryDescription.WithParameters(parameters);
            int edgeIndex = queryDescription.Edges.Count + 1;
            var edge = new EdgeDescription(
                edgeIndex,
                predecessorFactIndex,
                successorFactIndex,
                roleParameter);
            queryDescription = queryDescription.WithEdges(queryDescription.Edges.Add(edge));
            var context = new ResultDescriptionBuilderContext(queryDescription, KnownFacts, Path);
            return context;
        }

        public ResultDescriptionBuilderContext WithLabel(Label label, int factIndex)
        {
            // If we have not captured the known fact, add it now.
            if (!KnownFacts.ContainsKey(label.Name))
            {
                var factByLabel = KnownFacts.Add(label.Name, new FactDescription(label.Type, factIndex));
                // If we have not written the output, write it now.
                // Only write the output if we are not inside of an existential condition.
                // Use the prefix, which will be set for projections.
                if (Path.Count == 0)
                {
                    var prefix = "";
                    var output = new OutputDescription(prefix + label.Name, label.Type, factIndex);
                    var outputs = QueryDescription.Outputs.Add(output);
                    var queryDescription = QueryDescription.WithOutputs(outputs);
                    return new ResultDescriptionBuilderContext(queryDescription, factByLabel, Path);
                }
            }
            return this;
        }
    }
}