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

        [Fact]
        public async Task Projection_Query()
        {
            var airline = await j.Fact(new Airline("IA"));
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var passenger = await j.Fact(new Passenger(airline, user));
            await j.Fact(new PassengerName(passenger, "Joe", new PassengerName[0]));

            var namesOfPassenger = Given<Passenger>.Match((passenger, facts) =>
                from passengerName in facts.OfType<PassengerName>()
                where passengerName.passenger == passenger
                where !(
                    from next in facts.OfType<PassengerName>()
                    where next.prior.Contains(passengerName)
                    select next
                ).Any()
                select passengerName
            );

            var passengers = await j.Query(airline, Given<Airline>.Match((airline, facts) =>
                from passenger in facts.OfType<Passenger>()
                where passenger.airline == airline
                select new
                {
                    passenger,
                    names = facts.All(passenger, namesOfPassenger)
                }
            ));

            var result = passengers.Should().ContainSingle().Subject;
            result.names.Should().ContainSingle().Which
                .value.Should().Be("Joe");
        }
    }
}
