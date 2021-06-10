using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines
{
    public class Path
    {
        private readonly string tag;
        private readonly string targetType;
        private readonly string startingTag;
        private readonly ImmutableList<Step> steps;

        public Path(string tag, string targetType, string startingTag, ImmutableList<Step> steps)
        {
            this.tag = tag;
            this.targetType = targetType;
            this.startingTag = startingTag;
            this.steps = steps;
        }

        public string ToDescriptiveString()
        {
            string stepsDescriptiveString = string.Join(" ", steps
                .Select(step => step.ToDescriptiveString()));
            return $"    {tag}: {targetType} = {startingTag} {stepsDescriptiveString}\r\n";
        }
    }
}
