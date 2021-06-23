using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Pipelines
{
    public class Pipeline
    {
        private readonly string initialFactName;
        private readonly string initialFactType;
        private readonly ImmutableList<Path> paths;
        private readonly Projection projection;

        public static Pipeline FromInitialFact(string name, string type)
        {
            return new Pipeline(name, type, ImmutableList<Path>.Empty, new Projection(name));
        }

        private Pipeline(string initialFactName, string initialFactType, ImmutableList<Path> paths, Projection projection)
        {
            this.initialFactName = initialFactName;
            this.initialFactType = initialFactType;
            this.paths = paths;
            this.projection = projection;
        }

        public Pipeline WithPath(Path path)
        {
            return new Pipeline(initialFactName, initialFactType, paths.Add(path), new Projection(path.Tag));
        }

        public ImmutableList<Step> Linearize(string outerTag)
        {
            var tag = projection.Tag;
            ImmutableList<Step> steps = ImmutableList<Step>.Empty;
            while (tag != initialFactName)
            {
                var path = paths.Where(p => p.Tag == tag).Single();
                steps = path.Steps.AddRange(steps);
                if (tag == outerTag)
                {
                    break;
                }
                tag = path.StartingTag;
            }
            return steps;
        }

        public string ToDescriptiveString()
        {
            string pathDescriptiveString = string.Join("", paths
                .Select(path => path.ToDescriptiveString(1)));
            string projectionDescriptiveString = projection.ToDescriptiveString();
            return $"{initialFactName}: {initialFactType} {{\r\n{pathDescriptiveString}    {projectionDescriptiveString}\r\n}}";
        }

        public string ToOldDescriptiveString()
        {
            var tag = projection.Tag;
            ImmutableList<Step> steps = ImmutableList<Step>.Empty;
            while (tag != initialFactName)
            {
                var path = paths.Where(p => p.Tag == tag).Single();
                steps = path.Steps.AddRange(steps);
                tag = path.StartingTag;
            }
            string oldDescriptiveString = string.Join(" ", steps
                .Select(step => step.ToOldDescriptiveString()));
            return oldDescriptiveString;
        }
    }
}
