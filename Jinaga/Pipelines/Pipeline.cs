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
            return new Pipeline(name, type, ImmutableList<Path>.Empty, new SimpleProjection(name));
        }

        private Pipeline(string initialFactName, string initialFactType, ImmutableList<Path> paths, Projection projection)
        {
            this.initialFactName = initialFactName;
            this.initialFactType = initialFactType;
            this.paths = paths;
            this.projection = projection;
        }

        public Pipeline WithProjection(string name, string tag)
        {
            if (projection is SimpleProjection)
            {
                return new Pipeline(initialFactName, initialFactType, paths, new CompoundProjection().With(name, tag));
            }
            else if (projection is CompoundProjection compoundProjection)
            {
                return new Pipeline(initialFactName, initialFactType, paths, compoundProjection.With(name, tag));
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public Pipeline Compose(Pipeline pipeline)
        {
            if (this.initialFactName != pipeline.initialFactName)
            {
                throw new ArgumentException("Initial fact names to not agree");
            }
            if (this.initialFactType != pipeline.initialFactType)
            {
                throw new ArgumentException("Initial fact types to not agree");
            }

            var combinedPaths = paths
                .Union(pipeline.paths, new PathTagComparer())
                .ToImmutableList();
            return new Pipeline(initialFactName, initialFactType, combinedPaths, projection);
        }

        public Pipeline WithPath(Path path)
        {
            return new Pipeline(initialFactName, initialFactType, paths.Add(path), new SimpleProjection(path.Tag));
        }

        public ImmutableList<Step> Linearize(string outerTag)
        {
            if (projection is SimpleProjection simpleProjection)
            {
                var tag = simpleProjection.Tag;
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
            else
            {
                throw new NotImplementedException();
            }
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
            var steps = Linearize(null);
            string oldDescriptiveString = string.Join(" ", steps
                .Select(step => step.ToOldDescriptiveString()));
            return oldDescriptiveString;
        }
    }
}
