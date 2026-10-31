using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Store.SQLite.Description
{
    internal class QueryDescription
    {
        public static readonly QueryDescription Empty = new QueryDescription(
            ImmutableList<InputDescription>.Empty,
            ImmutableList<object>.Empty,
            ImmutableList<OutputDescription>.Empty,
            ImmutableList<FactDescription>.Empty,
            ImmutableList<EdgeDescription>.Empty,
            ImmutableList<ExistentialConditionDescription>.Empty,
            ImmutableList<FactEqualityDescription>.Empty);

        public ImmutableList<InputDescription> Inputs { get; }
        public ImmutableList<object> Parameters { get; }
        public ImmutableList<OutputDescription> Outputs { get; }
        public ImmutableList<FactDescription> Facts { get; }
        public ImmutableList<EdgeDescription> Edges { get; }
        public ImmutableList<ExistentialConditionDescription> ExistentialConditions { get; }
        public ImmutableList<FactEqualityDescription> FactEqualities { get; }

        private QueryDescription(ImmutableList<InputDescription> inputs, ImmutableList<object> parameters, ImmutableList<OutputDescription> outputs, ImmutableList<FactDescription> facts, ImmutableList<EdgeDescription> edges, ImmutableList<ExistentialConditionDescription> existentialConditions, ImmutableList<FactEqualityDescription> factEqualities)
        {
            Inputs = inputs;
            Parameters = parameters;
            Outputs = outputs;
            Facts = facts;
            Edges = edges;
            ExistentialConditions = existentialConditions;
            FactEqualities = factEqualities;
        }
        
        public bool IsSatisfiable()
        {
            return Inputs.Any();
        }

        public int OutputLength()
        {
            return Inputs.Count + Outputs.Count;
        }

        public QueryDescription WithInputs(ImmutableList<InputDescription> inputs)
        {
            return new QueryDescription(inputs, Parameters, Outputs, Facts, Edges, ExistentialConditions, FactEqualities);
        }

        public QueryDescription WithParameters(ImmutableList<object> parameters)
        {
            return new QueryDescription(Inputs, parameters, Outputs, Facts, Edges, ExistentialConditions, FactEqualities);
        }

        public QueryDescription WithFacts(ImmutableList<FactDescription> facts)
        {
            return new QueryDescription(Inputs, Parameters, Outputs, facts, Edges, ExistentialConditions, FactEqualities);
        }

        public QueryDescription WithEdges(ImmutableList<EdgeDescription> edgeDescriptions)
        {
            return new QueryDescription(Inputs, Parameters, Outputs, Facts, edgeDescriptions, ExistentialConditions, FactEqualities);
        }

        public QueryDescription WithOutputs(ImmutableList<OutputDescription> outputs)
        {
            return new QueryDescription(Inputs, Parameters, outputs, Facts, Edges, ExistentialConditions, FactEqualities);
        }

        public QueryDescription WithExistentialConditions(ImmutableList<ExistentialConditionDescription> existentialConditions)
        {
            return new QueryDescription(Inputs, Parameters, Outputs, Facts, Edges, existentialConditions, FactEqualities);
        }

        public QueryDescription WithFactEqualities(ImmutableList<FactEqualityDescription> factEqualities)
        {
            return new QueryDescription(Inputs, Parameters, Outputs, Facts, Edges, ExistentialConditions, factEqualities);
        }
  }
}