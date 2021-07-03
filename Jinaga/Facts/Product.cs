using System.Collections.Immutable;

namespace Jinaga.Facts
{
    public class Product
    {
        private readonly ImmutableDictionary<string, Fact> facts;

        public Product(ImmutableDictionary<string, Fact> facts)
        {
            this.facts = facts;
        }

        public Fact GetFact(string tag)
        {
            return facts[tag];
        }

        public Product With(string tag, Fact fact)
        {
            return new Product(facts.Add(tag, fact));
        }

        public static Product Init(string tag, Fact fact)
        {
            return new Product(ImmutableDictionary<string, Fact>.Empty.Add(tag, fact));
        }
    }
}