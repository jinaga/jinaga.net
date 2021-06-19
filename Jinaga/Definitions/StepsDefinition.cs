using System.Collections.Immutable;
using Jinaga.Pipelines;

namespace Jinaga.Definitions
{
    public class StepsDefinition
    {
        private readonly string parameterName;
        private readonly string parameterType;
        private readonly string startingTag;
        private readonly ImmutableList<Step> steps;

        public StepsDefinition(string parameterName, string parameterType, string startingTag, ImmutableList<Step> steps)
        {
            this.parameterName = parameterName;
            this.parameterType = parameterType;
            this.startingTag = startingTag;
            this.steps = steps;
        }
    }
}
