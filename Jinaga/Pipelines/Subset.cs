using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Visualizers;
using System.Collections.Immutable;
using System.Linq;

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

        public FactReferenceTuple Of(Product product)
        {
            var result = names.Aggregate(
                FactReferenceTuple.Empty,
                (tuple, name) => tuple.Add(name, product.GetFactReference(name)));
            return result;
        }

        public override string ToString()
        {
            return names.Join(", ");
        }
    }
}