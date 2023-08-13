using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Facts;
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

        public bool Contains(string name) => elements.ContainsKey(name);

        public Element GetElement(string name)
        {
            if (elements.TryGetValue(name, out var element))
            {
                return element;
            }
            else
            {
                throw new ArgumentException($"The product {this} does not contain an element named {name}.");
            }
        }

        public FactReference GetFactReference(string name)
        {
            if (elements.TryGetValue(name, out var element))
            {
                if (element is SimpleElement simple)
                {
                    return simple.FactReference;
                }
                else
                {
                    throw new InvalidOperationException($"The element {name} is not a simple fact reference");
                }
            }
            else
            {
                var names = string.Join(", ", elements.Keys);
                throw new InvalidOperationException($"The element {name} is not in the product: {names}");
            }
        }

        public IEnumerable<FactReference> GetFactReferences()
        {
            return elements.Values.SelectMany(e => e.GetFactReferences());
        }

        public Product With(string name, Element element)
        {
            return new Product(elements.SetItem(name, element));
        }

        public FactReferenceTuple GetAnchor()
        {
            var anchor = elements
                .Where(pair => pair.Value is SimpleElement)
                .Aggregate(
                    FactReferenceTuple.Empty,
                    (tuple, pair) => tuple.Add(pair.Key, ((SimpleElement)pair.Value).FactReference)
                );
            return anchor;
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

        public override string ToString()
        {
            var names = string.Join(", ", elements.Keys);
            return $"({names})";
        }
    }
}