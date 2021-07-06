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
            var fact = new AirlineDay(
                new Airline("value"),
                DateTime.Parse("2021-07-04T00:00:00.000Z")
            );
            var hash = ComputeHash(fact);

            hash.Should().Be("cQaErYsizavFrTIGjD1C0g3shMG/uq+hVUXzs/kCzcvev9gPrVDom3pbrszUsmeRelNv8bRdIvOb6AbaYrVC7w==");
        }

        [Fact]
        public void HashIntegerField()
        {
            var fact = new Flight(
                new AirlineDay(
                    new Airline("value"),
                    DateTime.Parse("2021-07-04T00:00:00.000Z")
                ),
                4247
            );
            var hash = ComputeHash(fact);

            hash.Should().Be("PyXT7pCvBq7Vw63kEZGgbIVJxqA7jhoO+QbmeM3YC9laayG0gjln58khyOd4D/cmxXzocPaIuwXGWusVJxqEjQ==");
        }

        [Fact]
        public void HashEmptyMultiplePredecessor()
        {
            var fact = new PassengerName(
                new Passenger(
                    new Airline("IA"),
                    new User("---PUBLIC KEY---")
                ),
                "Charles Rane",
                new PassengerName[0]
            );
            var hash = ComputeHash(fact);

            hash.Should().Be("GsMMA/8Nv401P6RXvugFYzYCemGehnXSFZuaKNcoVFoXKmxzMJkpqI9rs/SRlKHZlnRP1QsBxFWKFt6143OpYA==");
        }

        private static string ComputeHash(object fact)
        {
            return FactSerializer.Serialize(fact).Last.Hash;
        }
    }
}
