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
            return Products.GetHashCode();
        }
    }
}