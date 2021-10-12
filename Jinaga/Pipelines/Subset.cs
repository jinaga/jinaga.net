using System.Collections.Immutable;
using System.Linq;
using Jinaga.Products;
using Jinaga.Visualizers;

namespace Jinaga.Pipelines
{
    public class Subset
    {
        public static Subset Empty = new Subset(ImmutableList<string>.Empty);

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

        public Subset Add(string name)
        {
            return new Subset(names.Add(name));
        }

        public Product Of(Product product)
        {
            var result = names.Aggregate(
                Product.Empty,
                (sub, name) => sub.With(name, product.GetElement(name)));
            return result;
        }

        public override string ToString()
        {
            return names.Join(", ");
        }
    }
}