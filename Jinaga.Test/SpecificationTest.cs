using System.Linq;
using FluentAssertions;
using Jinaga.Pipelines;
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
                where flight.airlineDay.airline == airline
                select flight
            );
            Pipeline pipeline = specification.Compile();
            string descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"airline: Skylane.Airline {
    flight: Skylane.Flight = airline S.airline Skylane.Airline.Day S.airlineDay Skylane.Flight
    flight
}");
            string oldDescriptiveString = pipeline.ToOldDescriptiveString();
            oldDescriptiveString.Should().Be("S.airline F.type=\"Skylane.Airline.Day\" S.airlineDay F.type=\"Skylane.Flight\"");
        }

        [Fact]
        public void CanSpecifyPredecessors()
        {
            Specification<FlightCancellation, Flight> specification = Given<FlightCancellation>.Match((flightCancellation, facts) =>
                from flight in facts.OfType<Flight>()
                where flightCancellation.flight == flight
                select flight
            );
            Pipeline pipeline = specification.Compile();
            string descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"flightCancellation: Skylane.Flight.Cancellation {
    flight: Skylane.Flight = flightCancellation P.flight Skylane.Flight
    flight
}");
            string oldDescriptiveString = pipeline.ToOldDescriptiveString();
            oldDescriptiveString.Should().Be("P.flight F.type=\"Skylane.Flight\"");
        }

        [Fact]
        public void CanSpecifyPredecessorsShorthand()
        {
            Specification<FlightCancellation, Flight> specification = Given<FlightCancellation>.Match(flightCancellation =>
                flightCancellation.flight
            );
            Pipeline pipeline = specification.Compile();
            string descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"flightCancellation: Skylane.Flight.Cancellation {
    flight: Skylane.Flight = flightCancellation P.flight Skylane.Flight
    flight
}");
            string oldDescriptiveString = pipeline.ToOldDescriptiveString();
            oldDescriptiveString.Should().Be("P.flight F.type=\"Skylane.Flight\"");
        }

        [Fact]
        public void CanSpecifyNegativeExistentialConditions()
        {
            Specification<AirlineDay, Flight> activeFlights = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay == airlineDay

                where !(
                    from cancellation in facts.OfType<FlightCancellation>()
                    where cancellation.flight == flight
                    select cancellation
                ).Any()

                select flight
            );
            var pipeline = activeFlights.Compile();
            var descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"airlineDay: Skylane.Airline.Day {
    flight: Skylane.Flight = airlineDay S.airlineDay Skylane.Flight N(
        S.flight Skylane.Flight.Cancellation
    )
    flight
}");
            var oldDescriptiveString = pipeline.ToOldDescriptiveString();
            oldDescriptiveString.Should().Be("S.airlineDay F.type=\"Skylane.Flight\" N(S.flight F.type=\"Skylane.Flight.Cancellation\")");
        }

        [Fact]
        public void CanSpecifyNamedNegativeExistentialConditions()
        {
            Specification<AirlineDay, Flight> activeFlights = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay == airlineDay

                where !flight.IsCancelled

                select flight
            );
            var pipeline = activeFlights.Compile();
            var descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"airlineDay: Skylane.Airline.Day {
    flight: Skylane.Flight = airlineDay S.airlineDay Skylane.Flight N(
        S.flight Skylane.Flight.Cancellation
    )
    flight
}");
            var oldDescriptiveString = pipeline.ToOldDescriptiveString();
            oldDescriptiveString.Should().Be("S.airlineDay F.type=\"Skylane.Flight\" N(S.flight F.type=\"Skylane.Flight.Cancellation\")");
        }

        [Fact]
        public void CanSpecifyPositiveExistentialCondition()
        {
            Specification<Airline, Booking> bookingsToRefund = Given<Airline>.Match((airline, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay.airline == airline

                where (
                    from cancellation in facts.OfType<FlightCancellation>()
                    where cancellation.flight == flight
                    select cancellation
                ).Any()

                from booking in facts.OfType<Booking>()
                where booking.flight == flight

                where !(
                    from refund in facts.OfType<Refund>()
                    where refund.booking == booking
                    select refund
                ).Any()

                select booking
            );
            var pipeline = bookingsToRefund.Compile();
            var descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"airline: Skylane.Airline {
    flight: Skylane.Flight = airline S.airline Skylane.Airline.Day S.airlineDay Skylane.Flight E(
        S.flight Skylane.Flight.Cancellation
    )
    booking: Skylane.Booking = flight S.flight Skylane.Booking N(
        S.booking Skylane.Refund
    )
    booking
}");
        }

        [Fact]
        public void CanSpecifyNamedPositiveExistentialCondition()
        {
            Specification<Airline, Booking> bookingsToRefund = Given<Airline>.Match((airline, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay.airline == airline

                where flight.IsCancelled

                from booking in facts.OfType<Booking>()
                where booking.flight == flight

                where !(
                    from refund in facts.OfType<Refund>()
                    where refund.booking == booking
                    select refund
                ).Any()

                select booking
            );
        }

        [Fact]
        public void CanSpecifyProjection()
        {
            var bookingsToRefund = Given<Airline>.Match((airline, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay.airline == airline

                from cancellation in facts.OfType<FlightCancellation>()
                where cancellation.flight == flight

                from booking in facts.OfType<Booking>()
                where booking.flight == flight

                where !(
                    from refund in facts.OfType<Refund>()
                    where refund.booking == booking
                    select refund
                ).Any()

                select new
                {
                    Booking = booking,
                    Cancellation = cancellation
                }
            );
        }
    }
}
