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
            var queryDescription = WithInput(
                QueryDescription.WithParameters(parameters).WithFacts(facts), input);
            return new ResultDescriptionBuilderContext(queryDescription, KnownFacts.Add(label.Name, factDescription), Path);
        }

        /// <summary>
        /// Declares an input where the query reaches the fact it names: at the top level, or
        /// within the existential condition this context is building.
        /// </summary>
        /// <remarks>
        /// A given first referenced inside an existential condition is joined inside that
        /// condition's subquery and nowhere else. Declaring its input at the top level puts
        /// its type and hash in the outer where clause, which names a fact the outer query
        /// never joins, and the whole query fails with <c>no such column</c>. The
        /// specification that reaches this is one whose given is joined only from inside a
        /// negated condition, which a caller can write.
        /// </remarks>
        private QueryDescription WithInput(QueryDescription queryDescription, InputDescription input)
        {
            if (Path.Count == 0)
            {
                return queryDescription.WithInputs(queryDescription.Inputs.Add(input));
            }
            return queryDescription.WithExistentialConditions(
                ExistentialsWithInput(queryDescription.ExistentialConditions, input, Path));
        }

        private ImmutableList<ExistentialConditionDescription> ExistentialsWithInput(ImmutableList<ExistentialConditionDescription> existentialConditions, InputDescription input, ImmutableList<int> path)
        {
            int index = path[0];
            var existentialCondition = existentialConditions[index];
            var existentialConditionWithInput = path.Count == 1
                ? existentialCondition.WithInputs(existentialCondition.Inputs.Add(input))
                : existentialCondition.WithExistentialConditions(
                    ExistentialsWithInput(existentialCondition.ExistentialConditions, input, path.RemoveAt(0)));
            return existentialConditions
                .RemoveAt(index)
                .Insert(index, existentialConditionWithInput);
        }

        /// <summary>
        /// Binds a given to a fact the query already has, rather than to one of its own.
        /// </summary>
        /// <remarks>
        /// A path condition with no roles on either side is an equality between the facts
        /// two labels bind, not a step from one to the other, so it contributes no edge.
        /// When the unknown is already bound, the given on the right names that same fact:
        /// it adds that fact's type and hash as a constraint and no fact of its own. A fact
        /// of its own would be one that no edge joins, and the query would select and filter
        /// a column that no table in it supplies.
        /// </remarks>
        public ResultDescriptionBuilderContext WithInputParameterOnFact(Label label, int factTypeId, string hash, int factIndex)
        {
            int factTypeParameter = QueryDescription.Parameters.Count + 1;
            int factHashParameter = QueryDescription.Parameters.Count + 2;
            var input = new InputDescription(label.Name, label.Type, factIndex, factTypeParameter, factHashParameter);
            var parameters = QueryDescription.Parameters.Add(factTypeId).Add(hash);
            var queryDescription = WithInput(
                QueryDescription.WithParameters(parameters), input);
            return new ResultDescriptionBuilderContext(
                queryDescription,
                KnownFacts.Add(label.Name, new FactDescription(label.Type, factIndex)),
                Path);
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