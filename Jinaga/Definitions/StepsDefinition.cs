using System;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public class StepsDefinition
    {
        private readonly string parameterName;
        private readonly string startingTag;
        private readonly ImmutableList<Step> steps;

        public StepsDefinition(string parameterName, string startingTag, ImmutableList<Step> steps)
        {
            this.parameterName = parameterName;
            this.startingTag = startingTag;
            this.steps = steps;
        }

        public Pipeline CreatePipeline(string factType, ImmutableList<ConditionDefinition> conditions)
        {
            var allSteps = steps.AddRange(conditions.Select(condition => condition.CreateConditionalStep()));
            var path = new Path(parameterName, factType, startingTag, allSteps);
            var paths = ImmutableList<Path>.Empty.Add(path);
            var projection = new Projection(parameterName);
            var startingType = steps.First().InitialType;
            var pipeline = new Pipeline(startingTag, startingType, paths, projection);
            return pipeline;
        }
    }
}
