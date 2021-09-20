using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Pipelines;

namespace Jinaga.Products
{
    public class Product
    {
        public static Product Empty = new Product(ImmutableDictionary<string, Element>.Empty);
        private readonly ImmutableDictionary<string, Element> elements;

        private Product(ImmutableDictionary<string, Element> elements)
        {
            this.elements = elements;
        }

        public IEnumerable<string> Names => elements.Keys;

        public Element GetElement(string name) => elements[name];

        public Product With(string name, Element element)
        {
            return new Product(elements.SetItem(name, element));
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            
            var that = (Product)obj;
            var thoseReferences = that.elements
                .Select(pair => ComparablePair.From(pair.Key, pair.Value));
            var theseReferences = this.elements
                .Select(pair => ComparablePair.From(pair.Key, pair.Value));
            return thoseReferences.SetEquals(theseReferences);
        }

        public override int GetHashCode()
        {
            var theseReferences = this.elements
                .Select(pair => ComparablePair.From(pair.Key, pair.Value));
            return theseReferences.SetHash();
        }
    }
}