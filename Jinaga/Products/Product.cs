using System.Collections.Generic;
using System.Collections.Immutable;

namespace Jinaga.Products
{
    public class Product
    {
        public static Product Empty = new Product(ImmutableDictionary<string, Element>.Empty);
        private readonly ImmutableDictionary<string, Element> products;

        private Product(ImmutableDictionary<string, Element> products)
        {
            this.products = products;
        }

        public IEnumerable<string> Names => products.Keys;

        public Element GetElement(string name) => products[name];

        public Product With(string name, Element product)
        {
            return new Product(products.SetItem(name, product));
        }

        public override bool Equals(object obj)
        {
            return obj is Product composite &&
                EqualityComparer<ImmutableDictionary<string, Element>>.Default.Equals(products, composite.products);
        }

        public override int GetHashCode()
        {
            return -1937169414 + EqualityComparer<ImmutableDictionary<string, Element>>.Default.GetHashCode(products);
        }
    }
}