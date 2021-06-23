using System.Collections.Immutable;
using System.Linq;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public class StepsDefinition
    {
        private readonly string tag;
        private readonly SetDefinition startingSet;
        private readonly ImmutableList<Step> steps;

        public string Tag => tag;

        public string InitialFactName => startingSet?.Tag;

        public StepsDefinition(string tag, SetDefinition startingSet, ImmutableList<Step> steps)
        {
            this.tag = tag;
            this.startingSet = startingSet;
            this.steps = steps;
        }

        public Pipeline CreatePipeline(string factType, ImmutableList<ConditionDefinition> conditions)
        {
            if (startingSet != null)
            {
                var pipeline = startingSet.CreatePipeline();
                var allSteps = steps.AddRange(conditions.Select(condition => condition.CreateConditionalStep()));
                var path = new Path(tag, factType, startingSet.Tag, allSteps);
                
                return pipeline.WithPath(path);
            }
            else
            {
                return Pipeline.FromInitialFact(tag, factType);
            }
        }
    }
}
