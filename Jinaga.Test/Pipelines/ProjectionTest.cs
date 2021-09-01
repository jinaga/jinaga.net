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

        //[Fact]
        public void PipelineProjection_Collection()
        {
            var specification = Given<Airline>.Match((airline, facts) =>
                from passenger in facts.OfType<Passenger>()
                where passenger.airline == airline
                select new
                {
                    passenger,
                    names =
                        from passengerName in facts.OfType<PassengerName>()
                        where passengerName.passenger == passenger
                        select passengerName
                }
            );

            specification.Projection.ToDescriptiveString().Should().Be(@"{
        passenger = passenger
        names = [passengerName]
    }");
        }
    }
}
