using System;
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
            var fact = new Airline("value");
            var hash = ComputeHash(fact);

            hash.Should().Be("uXcsBceLFAkZdRD71Ztvc+QwASayHA0Zg7wC2mc3zl28N1hKTbGBfBA2OnEHAWo+0yYVeUnABMn9MCRH8cRHWg==");
        }

        [Fact]
        public void HashPredecessor()
        {
            var fact = new AirlineDay(new Airline("value"), DateTime.Parse("2021-07-04T00:00:00.000Z"));
            var hash = ComputeHash(fact);

            hash.Should().Be("cQaErYsizavFrTIGjD1C0g3shMG/uq+hVUXzs/kCzcvev9gPrVDom3pbrszUsmeRelNv8bRdIvOb6AbaYrVC7w==");
        }

        private static string ComputeHash(object fact)
        {
            return FactSerializer.Serialize(fact).Last().Reference.Hash;
        }
    }
}
