using System.Linq;
using System;
using Jinaga.Test.Model;
using Xunit;
using FluentAssertions;

namespace Jinaga.Test.Pipelines
{
    public class ProjectionTest
    {
        [Fact]
        public void PipelineProjection_Product()
        {
            var specification = Given<Airline>.Match((airline, facts) =>
                from passenger in facts.OfType<Passenger>()
                where passenger.airline == airline
                from passengerName in facts.OfType<PassengerName>()
                where passengerName.passenger == passenger
                select new
                {
                    passenger,
                    passengerName
                }
            );

            specification.ToDescriptiveString().Should().Be(@"(airline: Skylane.Airline) {
    passenger: Skylane.Passenger [
        passenger->airline: Skylane.Airline = airline
    ]
    passengerName: Skylane.Passenger.Name [
        passengerName->passenger: Skylane.Passenger = passenger
    ]
} => {
    passenger = passenger
    passengerName = passengerName
}
".Replace("\r", ""));
        }

        [Fact]
        public void PipelineProjection_Observe()
        {
            Specification<Passenger, PassengerName> namesOfPassenger = Given<Passenger>.Match((passenger, facts) =>
                from passengerName in facts.OfType<PassengerName>()
                where passengerName.passenger == passenger
                where !(
                    from next in facts.OfType<PassengerName>()
                    where next.prior.Contains(passengerName)
                    select next
                ).Any()
                select passengerName
            );

            var specification = Given<Airline>.Match((airline, facts) =>
                from passenger in facts.OfType<Passenger>()
                where passenger.airline == airline
                select new
                {
                    passenger,
                    names = facts.Observable(passenger, namesOfPassenger)
                }
            );

            specification.ToDescriptiveString().Should().Be(@"(airline: Skylane.Airline) {
    passenger: Skylane.Passenger [
        passenger->airline: Skylane.Airline = airline
    ]
} => {
    names = {
        passengerName: Skylane.Passenger.Name [
            passengerName->passenger: Skylane.Passenger = passenger
            !E {
                next: Skylane.Passenger.Name [
                    next->prior: Skylane.Passenger.Name = passengerName
                ]
            }
        ]
    } => passengerName
    passenger = passenger
}
".Replace("\r", ""));
        }

        [Fact]
        public void Projection_Field()
        {
            var namesOfPassenger = Given<Passenger>.Match((passenger, facts) =>
                from passengerName in facts.OfType<PassengerName>()
                where passengerName.passenger == passenger
                where !(
                    from next in facts.OfType<PassengerName>()
                    where next.prior.Contains(passengerName)
                    select next
                ).Any()
                select passengerName.value
            );

            var expected = @"(passenger: Skylane.Passenger) {
    passengerName: Skylane.Passenger.Name [
        passengerName->passenger: Skylane.Passenger = passenger
        !E {
            next: Skylane.Passenger.Name [
                next->prior: Skylane.Passenger.Name = passengerName
            ]
        }
    ]
} => passengerName.value
".Replace("\r", "");

            namesOfPassenger.ToDescriptiveString().Should().Be(expected);
        }

        [Fact]
        public void Projection_InlineCollection()
        {
            var passengers = Given<Airline>.Match((airline, facts) =>
                from passenger in facts.OfType<Passenger>()
                where passenger.airline == airline
                select new
                {
                    passenger,
                    names = facts.Observable(
                        from passengerName in facts.OfType<PassengerName>()
                        where passengerName.passenger == passenger
                        where !(
                            from next in facts.OfType<PassengerName>()
                            where next.prior.Contains(passengerName)
                            select next
                        ).Any()
                        select passengerName.value
                    )
                }
            );

            var expected = @"(airline: Skylane.Airline) {
    passenger: Skylane.Passenger [
        passenger->airline: Skylane.Airline = airline
    ]
} => {
    names = {
        passengerName: Skylane.Passenger.Name [
            passengerName->passenger: Skylane.Passenger = passenger
            !E {
                next: Skylane.Passenger.Name [
                    next->prior: Skylane.Passenger.Name = passengerName
                ]
            }
        ]
    } => passengerName.value
    passenger = passenger
}
";
            passengers.ToDescriptiveString().Should().Be(expected.Replace("\r", ""));
        }

        [Fact]
        public void Projection_InlineIQueryableCollection()
        {
            var passengers = Given<Airline>.Match((airline, facts) =>
                from passenger in facts.OfType<Passenger>()
                where passenger.airline == airline
                select new
                {
                    passenger,
                    names =
                        from passengerName in facts.OfType<PassengerName>()
                        where passengerName.passenger == passenger
                        where !(
                            from next in facts.OfType<PassengerName>()
                            where next.prior.Contains(passengerName)
                            select next
                        ).Any()
                        select passengerName.value
                }
            );

            var expected = @"(airline: Skylane.Airline) {
    passenger: Skylane.Passenger [
        passenger->airline: Skylane.Airline = airline
    ]
} => {
    names = {
        passengerName: Skylane.Passenger.Name [
            passengerName->passenger: Skylane.Passenger = passenger
            !E {
                next: Skylane.Passenger.Name [
                    next->prior: Skylane.Passenger.Name = passengerName
                ]
            }
        ]
    } => passengerName.value
    passenger = passenger
}
";
            passengers.ToDescriptiveString().Should().Be(expected.Replace("\r", ""));
        }
    }
}
