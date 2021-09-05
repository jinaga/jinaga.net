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

            specification.Projection.ToDescriptiveString().Should().Be(@"{
        passenger = passenger
        passengerName = passengerName
    }");
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
                    names = facts.Observe(passenger, namesOfPassenger)
                }
            );

            specification.Pipeline.ToDescriptiveString().Should().Be(@"airline: Skylane.Airline {
    passenger: Skylane.Passenger = airline S.airline Skylane.Passenger
    passengerName: Skylane.Passenger.Name = passenger S.passenger Skylane.Passenger.Name
    N(
        passengerName: Skylane.Passenger.Name {
            next: Skylane.Passenger.Name = passengerName S.prior Skylane.Passenger.Name
        }
    )
}
");
            specification.Projection.ToDescriptiveString().Should().Be(@"{
        names = [passengerName]
        passenger = passenger
    }");
        }
    }
}
