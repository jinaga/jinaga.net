using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines
{
    public class Path
    {
        public string Tag { get; }
        public string TargetType { get; }
        public string StartingTag { get; }
        public ImmutableList<Step> Steps { get; }

        public Path(string tag, string targetType, string startingTag, ImmutableList<Step> steps)
        {
            Tag = tag;
            TargetType = targetType;
            StartingTag = startingTag;
            Steps = steps;
        }

        public string ToDescriptiveString(int depth = 1)
        {
            string stepsDescriptiveString = string.Join(" ", Steps
                .Select(step => step.ToDescriptiveString(depth)));
            string indent = String.Join("", Enumerable.Repeat("    ", depth).ToArray());
            return $"{indent}{Tag}: {TargetType} = {StartingTag} {stepsDescriptiveString}\r\n";
        }
    }
}
