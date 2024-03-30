using System;
using System.Linq;
using Jinaga.Observers;
using Jinaga.Test.Model;

namespace Jinaga.Test.Specifications.Specifications
{
    public class ProjectionTest
    {
        private readonly JinagaClient j;

        public ProjectionTest()
        {
            j = JinagaTest.Create();
        }

        private Specification<Passenger, PassengerName> namesOfPassenger = Given<Passenger>.Match((passenger, facts) =>
            from passengerName in facts.OfType<PassengerName>()
            where passengerName.passenger == passenger
            where !(
                from next in facts.OfType<PassengerName>()
                where next.prior.Contains(passengerName)
                select next
            ).Any()
            select passengerName
        );

        [Fact]
        public async Task Projection_Query()
        {
            var airline = await j.Fact(new Airline("IA"));
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var passenger = await j.Fact(new Passenger(airline, user));
            await j.Fact(new PassengerName(passenger, "Joe", new PassengerName[0]));

            var passengers = await j.Query(Given<Airline>.Match((airline, facts) =>
                from passenger in facts.OfType<Passenger>()
                where passenger.airline == airline
                select new
                {
                    passenger,
                    names = facts.Observable(passenger, namesOfPassenger)
                }
            ), airline);

            var result = passengers.Should().ContainSingle().Subject;
            result.names.Should().ContainSingle().Which
                .value.Should().Be("Joe");
        }

        [Fact]
        public async Task Projection_CanBeRecord()
        {
            var airline = await j.Fact(new Airline("IA"));
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var passenger = await j.Fact(new Passenger(airline, user));
            await j.Fact(new PassengerName(passenger, "Joe", new PassengerName[0]));

            var passengers = await j.Query(Given<Airline>.Match((airline, facts) =>
                from passenger in facts.OfType<Passenger>()
                where passenger.airline == airline
                select new PassengerProjection(
                    passenger,
                    facts.Observable(passenger, namesOfPassenger)
                )
                {
                    PassengerID = j.Hash(passenger)
                }
            ), airline);

            var result = passengers.Should().ContainSingle().Subject;
            result.names.Should().ContainSingle().Which
                .value.Should().Be("Joe");
        }

        [Fact]
        public async Task Projection_CanBeField()
        {
            var airline = await j.Fact(new Airline("IA"));
            var user = await j.Fact(new User("--- PUBLIC KEY ---"));
            var passenger = await j.Fact(new Passenger(airline, user));
            await j.Fact(new PassengerName(passenger, "Joe", new PassengerName[0]));

            var passengerNames = await j.Query(Given<Passenger>.Match((passenger, facts) =>
                from passengerName in facts.OfType<PassengerName>()
                where passengerName.passenger == passenger
                where !(
                    from next in facts.OfType<PassengerName>()
                    where next.prior.Contains(passengerName)
                    select next
                ).Any()
                select passengerName.value
            ), passenger);

            var result = passengerNames.Should().ContainSingle().Subject;
            result.Should().Be("Joe");
        }
    }

    record PassengerProjection(Passenger passenger, IObservableCollection<PassengerName> names)
    {
        public string Name => names.Select(name => name.value).FirstOrDefault();

        public string PassengerID { get; internal set; }
    }
}
