using System;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Facts;
using Jinaga.Visualizers;

namespace Jinaga.Pipelines
{
    public class Subset
    {
        private readonly ImmutableList<string> names;

        private Subset(ImmutableList<string> names)
        {
            this.names = names;
        }

        public static Subset FromPipeline(Pipeline pipeline)
        {
            var startNames = pipeline.Starts.Select(start => start.Name);
            var pathStartNames = pipeline.Paths.Select(path => path.Start.Name);
            var pathTargetNames = pipeline.Paths.Select(path => path.Target.Name);
            return new Subset(startNames.Union(pathStartNames).Union(pathTargetNames).ToImmutableList());
        }

        internal Product Of(Product product)
        {
            var result = names.Aggregate(
                Product.Empty,
                (sub, name) => sub.With(name, product.GetFactReference(name)));
            return result;
        }

        public override string ToString()
        {
            return names.Join(", ");
        }
    }
}