using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Facts;
using Jinaga.Pipelines;

namespace Jinaga.Products
{
    public class CollectionElement : Element
    {
        public ImmutableList<Product> Products { get; }

        public CollectionElement(ImmutableList<Product> products)
        {
            Products = products;
        }

        override public IEnumerable<FactReference> GetFactReferences()
        {
            return Products
                .SelectMany(p => p.Names.Select(n => p.GetElement(n)))
                .SelectMany(e => e.GetFactReferences());
        }

        public override bool Equals(object obj)
        {
            return obj is CollectionElement collection &&
                Products.SetEquals(collection.Products);
        }

        public override int GetHashCode()
        {
            // Hash the products as a set, the way Equals compares them: independent of
            // order, and with duplicates removed so that XOR does not cancel them.
            return Products.Distinct().SetHash();
        }
    }
}