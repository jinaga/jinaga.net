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

        public Subset Add(string name)
        {
            if (names.Contains(name))
            {
                return this;
            }
            else
            {
                return new Subset(names.Add(name));
            }
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