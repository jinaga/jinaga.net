using Jinaga.Pipelines;
using Jinaga.Store.SQLite.Description;
using System.Collections.Immutable;
using System.Linq;

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
            (var newExistentialConditions, var newPath) = ExistentialsWithNewCondition(QueryDescription.ExistentialConditions, exists, Path);
            var queryDescription = QueryDescription.WithExistentialConditions(newExistentialConditions);
            return new ResultDescriptionBuilderContext(queryDescription, KnownFacts, newPath);
        }

        private (ImmutableList<ExistentialConditionDescription> newExistentialCondition, ImmutableList<int> newPath) ExistentialsWithNewCondition(ImmutableList<ExistentialConditionDescription> existentialConditions, bool exists, ImmutableList<int> path)
        {
            if (path.Count == 0)
            {
                var newPath = Path.Add(existentialConditions.Count);
                var existentialCondition = new ExistentialConditionDescription(
                    exists,
                    ImmutableList<InputDescription>.Empty,
                    ImmutableList<EdgeDescription>.Empty,
                    ImmutableList<ExistentialConditionDescription>.Empty);
                var existentialConditionsWithNewCondition = existentialConditions.Add(existentialCondition);
                return (existentialConditionsWithNewCondition, newPath);
            }
            else
            {
                int index = path[0];
                var existentialCondition = existentialConditions[index];
                (var newExistentialConditions, var newPath) = ExistentialsWithNewCondition(existentialCondition.ExistentialConditions, exists, path.RemoveAt(0));
                var newExistentialCondition = existentialCondition.WithExistentialConditions(newExistentialConditions);
                var existentialConditionsWithNewCondition = existentialConditions
                    .RemoveAt(index)
                    .Insert(index, newExistentialCondition);
                return (existentialConditionsWithNewCondition, newPath);
            }
        }

        public ResultDescriptionBuilderContext WithQueryDescription(QueryDescription queryDescription)
        {
            return new ResultDescriptionBuilderContext(queryDescription, KnownFacts, Path);
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
            var inputs = QueryDescription.Inputs.Add(input);
            var queryDescription = QueryDescription.WithInputs(inputs).WithParameters(parameters).WithFacts(facts);
            return new ResultDescriptionBuilderContext(queryDescription, KnownFacts.Add(label.Name, factDescription), Path);
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
            int edgeIndex = queryDescription.Edges.Count + CountEdges(QueryDescription.ExistentialConditions) + 1;
            var edge = new EdgeDescription(
                edgeIndex,
                predecessorFactIndex,
                successorFactIndex,
                roleParameter);
            if (Path.Count == 0)
            {
                queryDescription = queryDescription.WithEdges(queryDescription.Edges.Add(edge));
                var context = new ResultDescriptionBuilderContext(queryDescription, KnownFacts, Path);
                return context;
            }
            else
            {
                var existentialConditions = ExistentialsWithEdge(QueryDescription.ExistentialConditions, edge, Path);
                queryDescription = queryDescription.WithExistentialConditions(existentialConditions);
                var context = new ResultDescriptionBuilderContext(queryDescription, KnownFacts, Path);
                return context;
            }
        }

        private int CountEdges(ImmutableList<ExistentialConditionDescription> existentialConditions)
        {
            return existentialConditions
                .Select(ec => ec.Edges.Count + CountEdges(ec.ExistentialConditions))
                .Sum();
        }

        private ImmutableList<ExistentialConditionDescription> ExistentialsWithEdge(ImmutableList<ExistentialConditionDescription> existentialConditions, EdgeDescription edge, ImmutableList<int> path)
        {
            int index = path[0];
            var existentialCondition = existentialConditions[index];
            if (path.Count == 1)
            {
                var edges = existentialCondition.Edges.Add(edge);
                var existentialConditionWithEdge = existentialCondition.WithEdges(edges);
                return existentialConditions
                    .RemoveAt(index)
                    .Insert(index, existentialConditionWithEdge);
            }
            else
            {
                var existentialConditionsWithEdge = ExistentialsWithEdge(existentialCondition.ExistentialConditions, edge, path.RemoveAt(0));
                var existentialConditionWithExistentialConditions = existentialCondition.WithExistentialConditions(existentialConditionsWithEdge);
                return existentialConditions
                    .RemoveAt(index)
                    .Insert(index, existentialConditionWithExistentialConditions);
            }
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
                else
                {
                    return new ResultDescriptionBuilderContext(QueryDescription, factByLabel, Path);
                }
            }
            return this;
        }

        public ResultDescriptionBuilderContext WithoutLabel(Label unknown)
        {
            if (KnownFacts.ContainsKey(unknown.Name))
            {
                var factByLabel = KnownFacts.Remove(unknown.Name);
                return new ResultDescriptionBuilderContext(QueryDescription, factByLabel, Path);
            }
            return this;
        }
    }
}