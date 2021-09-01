using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jinaga.Test.Model;
using Jinaga.UnitTest;
using Xunit;

namespace Jinaga.Test
{
    public class ProjectionTest
    {
        private readonly Jinaga j;

        public ProjectionTest()
        {
            j = JinagaTest.Create();
        }

        //[Fact]
        public async Task Projection_FirstOrDefault()
        {
            var airline = await j.Fact(new Airline("IA"));
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var passenger = await j.Fact(new Passenger(airline, user));
            await j.Fact(new PassengerName(passenger, "Joe", new PassengerName[0]));

            var passengers = await j.Query(airline, Given<Airline>.Match((airline, facts) =>
                from passenger in facts.OfType<Passenger>()
                where passenger.airline == airline
                select new
                {
                    passenger,
                    name = (
                        from passengerName in facts.OfType<PassengerName>()
                        where passengerName.passenger == passenger
                        select passengerName.value
                    ).FirstOrDefault()
                }
            ));

            passengers.Should().ContainSingle().Which
                .name.Should().Be("Joe");
        }
    }
}
