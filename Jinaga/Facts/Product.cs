using System.Collections.Immutable;

namespace Jinaga.Facts
{
    public class Product
    {
        public static Product Empty = new Product(ImmutableDictionary<string, FactReference>.Empty);

        private readonly ImmutableDictionary<string, FactReference> factReferencesByTag;

        public Product(ImmutableDictionary<string, FactReference> factReferencesByTag)
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
    }
}