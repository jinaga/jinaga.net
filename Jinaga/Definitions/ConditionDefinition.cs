using System.Collections.Immutable;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public class ConditionDefinition
    {
        private SetDefinition set;
        private bool exists;

        public string InitialFactName => set.InitialFactName;

        public ConditionDefinition(SetDefinition set, bool exists)
        {
            this.set = set;
            this.exists = exists;
        }

        public ConditionDefinition Invert()
        {
            return new ConditionDefinition(set, exists: false);
        }

        public Step CreateConditionalStep()
        {
            var pipeline = set.CreatePipeline();
            ImmutableList<Step> steps = pipeline.Linearize();
            return new ConditionalStep(steps, exists);
        }
    }
}
