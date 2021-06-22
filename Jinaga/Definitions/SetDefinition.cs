using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public abstract class SetDefinition
    {
        public abstract SetDefinition WithSteps(StepsDefinition steps);
        public abstract SetDefinition WithCondition(ConditionDefinition condition);
        public abstract SetDefinition Compose(SetDefinition continuation, ProjectionDefinition projection);
        public abstract Pipeline CreatePipeline();
    }
}
