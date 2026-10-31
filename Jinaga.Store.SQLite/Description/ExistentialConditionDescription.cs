using System.Collections.Immutable;

namespace Jinaga.Store.SQLite.Description
{
    internal class ExistentialConditionDescription
    {
        public bool Exists { get; }
        public ImmutableList<InputDescription> Inputs { get; }
        public ImmutableList<EdgeDescription> Edges { get; }
        public ImmutableList<ExistentialConditionDescription> ExistentialConditions { get; }
        public ImmutableList<FactEqualityDescription> FactEqualities { get; }

        public ExistentialConditionDescription(bool exists, ImmutableList<InputDescription> inputs, ImmutableList<EdgeDescription> edges, ImmutableList<ExistentialConditionDescription> existentialConditions)
            : this(exists, inputs, edges, existentialConditions, ImmutableList<FactEqualityDescription>.Empty)
        {
        }

        private ExistentialConditionDescription(bool exists, ImmutableList<InputDescription> inputs, ImmutableList<EdgeDescription> edges, ImmutableList<ExistentialConditionDescription> existentialConditions, ImmutableList<FactEqualityDescription> factEqualities)
        {
            Exists = exists;
            Inputs = inputs;
            Edges = edges;
            ExistentialConditions = existentialConditions;
            FactEqualities = factEqualities;
        }

        public ExistentialConditionDescription WithInputs(ImmutableList<InputDescription> inputs)
        {
            return new ExistentialConditionDescription(Exists, inputs, Edges, ExistentialConditions, FactEqualities);
        }

        public ExistentialConditionDescription WithEdges(ImmutableList<EdgeDescription> edges)
        {
            return new ExistentialConditionDescription(Exists, Inputs, edges, ExistentialConditions, FactEqualities);
        }

        public ExistentialConditionDescription WithExistentialConditions(ImmutableList<ExistentialConditionDescription> existentialConditionsWithEdge)
        {
            return new ExistentialConditionDescription(Exists, Inputs, Edges, existentialConditionsWithEdge, FactEqualities);
        }

        public ExistentialConditionDescription WithFactEqualities(ImmutableList<FactEqualityDescription> factEqualities)
        {
            return new ExistentialConditionDescription(Exists, Inputs, Edges, ExistentialConditions, factEqualities);
        }
    }
}
