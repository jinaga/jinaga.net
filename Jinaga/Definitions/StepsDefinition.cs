using System;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public class StepsDefinition
    {
        private readonly string tag;
        private readonly string initialFactName;
        private readonly ImmutableList<Step> steps;

        public string Tag => tag;

        public StepsDefinition(string tag, string initialFactName, ImmutableList<Step> steps)
        {
            this.tag = tag;
            this.initialFactName = initialFactName;
            this.steps = steps;
        }

        public Pipeline CreatePipeline(string factType, ImmutableList<ConditionDefinition> conditions)
        {
            var allSteps = steps.AddRange(conditions.Select(condition => condition.CreateConditionalStep()));
            var path = new Path(tag, factType, initialFactName, allSteps);
            var paths = ImmutableList<Path>.Empty.Add(path);
            var projection = new Projection(tag);
            var initialFactType = steps.First().InitialType;
            var pipeline = new Pipeline(initialFactName, initialFactType, paths, projection);
            return pipeline;
        }
    }
}
