using System;
using System.Linq;
using Jinaga.Test.Model;
using Xunit;

namespace Jinaga.Test
{
    public class SpecificationTest
    {
        [Fact]
        public void CanSpecifySuccessors()
        {
            Specification<Airline, Flight> specification = Given<Airline>.Match((airline, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.AirlineDay.Airline == airline
                select flight
            );
        }

        [Fact]
        public void CanSpecifyPredecessors()
        {
            Specification<FlightCancellation, Flight> specification = Given<FlightCancellation>.Match((flightCancellation, facts) =>
                from flight in facts.OfType<Flight>()
                where flightCancellation.Flight == flight
                select flight
            );
        }

        [Fact]
        public void CanSpecifyExistentialConditions()
        {
            Specification<AirlineDay, Flight> activeFlights = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.AirlineDay == airlineDay
                where facts.None(
                    from cancellation in facts.OfType<FlightCancellation>()
                    where cancellation.Flight == flight
                    select cancellation
                )
                select flight
            );
        }
    }
}
