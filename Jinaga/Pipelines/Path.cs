using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines
{
    public class Path
    {
        public string Tag { get; }
        private readonly string targetType;
        public string StartingTag { get; }
        public ImmutableList<Step> Steps { get; }

        public Path(string tag, string targetType, string startingTag, ImmutableList<Step> steps)
        {
            Tag = tag;
            this.targetType = targetType;
            StartingTag = startingTag;
            Steps = steps;
        }

        public string ToDescriptiveString()
        {
            string stepsDescriptiveString = string.Join(" ", Steps
                .Select(step => step.ToDescriptiveString()));
            return $"    {Tag}: {targetType} = {StartingTag} {stepsDescriptiveString}\r\n";
        }
    }
}
