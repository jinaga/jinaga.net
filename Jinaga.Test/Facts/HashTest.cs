using System.Linq;
using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Test.Model;
using Xunit;

namespace Jinaga.Test.Facts
{
    public class HashTest
    {
        [Fact]
        public void HashStringField()
        {
            var facts = FactSerializer.Serialize(new Airline("value"));
            var airline = facts.Last();
            var hash = airline.Reference.Hash;

            hash.Should().Be("uXcsBceLFAkZdRD71Ztvc+QwASayHA0Zg7wC2mc3zl28N1hKTbGBfBA2OnEHAWo+0yYVeUnABMn9MCRH8cRHWg==");
        }
    }
}
