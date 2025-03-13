using System.Linq;
using Jinaga.Facts;
using Jinaga.Serialization;
using Jinaga.Test.Model;

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
        public void HashEmptyPredecessorList()
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

        [Fact]
        public void HashSinglePredecessorList()
        {
            var passenger = new Passenger(
                new Airline("IA"),
                new User("---PUBLIC KEY---")
            );
            var first = new PassengerName(
                passenger,
                "Charles Rane",
                new PassengerName[0]
            );
            var second = new PassengerName(
                passenger,
                "Charley Rane",
                new PassengerName[] { first }
            );
            var hash = ComputeHash(second);

            hash.Should().Be("BYLtR7XddbhchlyBdGdrnRHGkPsDecynDjLHFvqtKH7zug46ymxNDpPC4QNb+T14Bhzs8M1F3VfCnlgzinNHPg==");
        }

        [Fact]
        public void HashMultiplePredecessorList()
        {
            var passenger = new Passenger(
                new Airline("IA"),
                new User("---PUBLIC KEY---")
            );
            var first = new PassengerName(
                passenger,
                "Charles Rane",
                new PassengerName[0]
            );
            var middle = Enumerable.Range(1, 10)
                .Select(id => new PassengerName(
                    passenger,
                    $"Charley Rane {id}",
                    new PassengerName[] { first }
                ))
                .ToArray();
            var last = new PassengerName(
                passenger,
                "Charley Rane",
                middle
            );
            var hash = ComputeHash(last);

            hash.Should().Be("4Os8M2Tt7+lCEe6WQ6iAJwQ/wbmK6CTLqwF8DCS6Bc4tgXE268BanI0sHDeSYhbKYbSDAyRzarMkrciveBoDTQ==");
        }

        private static string ComputeHash(object fact)
        {
            var collector = new Collector(SerializerCache.Empty, new());
            var reference = collector.Serialize(fact);
            return reference.Hash;
        }
    }
}
