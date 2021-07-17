using System;
using System.Linq;
using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Test.Model;
using Xunit;

namespace Jinaga.Test.Facts
{
    public class OptimizationTest
    {
        [Fact]
        public void Optimization_LimitVisits()
        {
            var passenger = new Passenger(new Airline("IA"), new User("--- PUBLIC KEY ---"));
            var firstName = new PassengerName(passenger, "George", new PassengerName[0]);
            var secondName = new PassengerName(passenger, "Jorge", new PassengerName[] { firstName });

            var collector = new Collector();
            var result = collector.Serialize(secondName);
            int factVisits = collector.FactVisitsCount;

            factVisits.Should().Be(5);
        }
    }
}
