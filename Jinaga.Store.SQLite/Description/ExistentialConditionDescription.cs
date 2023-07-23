using System.Collections.Immutable;

namespace Jinaga.Store.SQLite.Description
{
    internal class ExistentialConditionDescription
    {
        public bool Exists { get; }
        public ImmutableList<InputDescription> Inputs { get; }
        public ImmutableList<EdgeDescription> Edges { get; }
        public ImmutableList<ExistentialConditionDescription> ExistentialConditions { get; }

        public ExistentialConditionDescription(bool exists, ImmutableList<InputDescription> inputs, ImmutableList<EdgeDescription> edges, ImmutableList<ExistentialConditionDescription> existentialConditions)
        {
            Exists = exists;
            Inputs = inputs;
            Edges = edges;
            ExistentialConditions = existentialConditions;
        }

        public ExistentialConditionDescription WithEdges(ImmutableList<EdgeDescription> edges)
        {
            return new ExistentialConditionDescription(Exists, Inputs, edges, ExistentialConditions);
        }

        public ExistentialConditionDescription WithExistentialConditions(ImmutableList<ExistentialConditionDescription> existentialConditionsWithEdge)
        {
            return new ExistentialConditionDescription(Exists, Inputs, Edges, existentialConditionsWithEdge);
        }
    }
}