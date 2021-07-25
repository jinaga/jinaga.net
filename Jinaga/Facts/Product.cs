using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Pipelines;

namespace Jinaga.Facts
{
    public class Product
    {
        public static Product Empty = new Product(ImmutableDictionary<string, FactReference>.Empty);

        private readonly ImmutableDictionary<string, FactReference> factReferencesByTag;

        private Product(ImmutableDictionary<string, FactReference> factReferencesByTag)
        {
            this.factReferencesByTag = factReferencesByTag;
        }

        public FactReference GetFactReference(string tag)
        {
            return factReferencesByTag[tag];
        }

        public Product With(string tag, FactReference factReference)
        {
            return new Product(factReferencesByTag.Add(tag, factReference));
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            
            var that = (Product)obj;
            var thoseReferences = that.factReferencesByTag
                .Select(pair => ComparablePair.From(pair.Key, pair.Value));
            var theseReferences = this.factReferencesByTag
                .Select(pair => ComparablePair.From(pair.Key, pair.Value));
            return thoseReferences.SetEquals(theseReferences);
        }

        public override int GetHashCode()
        {
            var theseReferences = this.factReferencesByTag
                .Select(pair => ComparablePair.From(pair.Key, pair.Value));
            return theseReferences.SetHash();
        }
    }
}