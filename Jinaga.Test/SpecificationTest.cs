using System;
using System.Linq;
using FluentAssertions;
using Jinaga.Test.Model;
using Jinaga.Test.Model.Order;
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
            var pipeline = specification.Pipeline;
            string descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"airline: Skylane.Airline {
    flight: Skylane.Flight = airline S.airline Skylane.Airline.Day S.airlineDay Skylane.Flight
}
");
            string oldDescriptiveString = pipeline.ToOldDescriptiveString();
            oldDescriptiveString.Should().Be("S.airline F.type=\"Skylane.Airline.Day\" S.airlineDay F.type=\"Skylane.Flight\"");
        }

        [Fact]
        public void Specification_MissingJoin()
        {
            Func<Specification<Airline, Flight>> constructor = () =>
                Given<Airline>.Match((airline, facts) =>
                    from flight in facts.OfType<Flight>()
                    select flight
                );
            constructor.Should().Throw<SpecificationException>()
                .WithMessage(
                    "The variable \"flight\" should be joined to the parameter \"airline\". " +
                    "Consider \"where flight.airlineDay.airline == airline\".");
        }

        [Fact]
        public void Specification_MissingCollectionJoin()
        {
            Func<Specification<Item, Order>> constructor = () =>
                Given<Item>.Match((item, facts) =>
                    from order in facts.OfType<Order>()
                    select order
                );
            constructor.Should().Throw<SpecificationException>()
                .WithMessage(
                    "The variable \"order\" should be joined to the parameter \"item\". " +
                    "Consider \"where order.items.Contains(item)\"."
                );
        }

        [Fact]
        public void Specification_MissingJoinWithExtensionMethod()
        {
            Func<Specification<Airline, Flight>> constructor = () =>
                Given<Airline>.Match((airline, facts) => facts.OfType<Flight>());
            constructor.Should().Throw<SpecificationException>()
                .WithMessage(
                    "The set should be joined to the parameter \"airline\". " +
                    "Consider \"facts.OfType<Flight>(flight => flight.airlineDay.airline == airline)\".");
        }

        [Fact]
        public void CanSpecifyShortSuccessors()
        {
            Specification<Airline, Flight> specification = Given<Airline>.Match((airline, facts) =>
                facts.OfType<Flight>(flight => flight.airlineDay.airline == airline)
            );
            var pipeline = specification.Pipeline;
            string descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"airline: Skylane.Airline {
    flight: Skylane.Flight = airline S.airline Skylane.Airline.Day S.airlineDay Skylane.Flight
}
");
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
            var pipeline = specification.Pipeline;
            string descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"flightCancellation: Skylane.Flight.Cancellation {
    flight: Skylane.Flight = flightCancellation P.flight Skylane.Flight
}
");
            string oldDescriptiveString = pipeline.ToOldDescriptiveString();
            oldDescriptiveString.Should().Be("P.flight F.type=\"Skylane.Flight\"");
        }

        [Fact]
        public void CanSpecifyPredecessorsShorthand()
        {
            Specification<FlightCancellation, Flight> specification = Given<FlightCancellation>.Match(flightCancellation =>
                flightCancellation.flight
            );
            var pipeline = specification.Pipeline;
            string descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"flightCancellation: Skylane.Flight.Cancellation {
    flight: Skylane.Flight = flightCancellation P.flight Skylane.Flight
}
");
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
            var pipeline = activeFlights.Pipeline;
            var descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"airlineDay: Skylane.Airline.Day {
    flight: Skylane.Flight = airlineDay S.airlineDay Skylane.Flight
    N(
        flight: Skylane.Flight {
            cancellation: Skylane.Flight.Cancellation = flight S.flight Skylane.Flight.Cancellation
        }
    )
}
");
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
            var pipeline = activeFlights.Pipeline;
            var descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"airlineDay: Skylane.Airline.Day {
    flight: Skylane.Flight = airlineDay S.airlineDay Skylane.Flight
    N(
        flight: Skylane.Flight {
            cancellation: Skylane.Flight.Cancellation = flight S.flight Skylane.Flight.Cancellation
        }
    )
}
");
            var oldDescriptiveString = pipeline.ToOldDescriptiveString();
            oldDescriptiveString.Should().Be("S.airlineDay F.type=\"Skylane.Flight\" N(S.flight F.type=\"Skylane.Flight.Cancellation\")");
        }

        [Fact]
        public void CanSpecifyNamedShortNegativeExistentialConditions()
        {
            Specification<AirlineDay, Flight> activeFlights = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay == airlineDay

                where !flight.ShortIsCancelled

                select flight
            );
            var pipeline = activeFlights.Pipeline;
            var descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"airlineDay: Skylane.Airline.Day {
    flight: Skylane.Flight = airlineDay S.airlineDay Skylane.Flight
    N(
        flight: Skylane.Flight {
            cancellation: Skylane.Flight.Cancellation = flight S.flight Skylane.Flight.Cancellation
        }
    )
}
");
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
            var pipeline = bookingsToRefund.Pipeline;
            var descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"airline: Skylane.Airline {
    flight: Skylane.Flight = airline S.airline Skylane.Airline.Day S.airlineDay Skylane.Flight
    E(
        flight: Skylane.Flight {
            cancellation: Skylane.Flight.Cancellation = flight S.flight Skylane.Flight.Cancellation
        }
    )
    booking: Skylane.Booking = flight S.flight Skylane.Booking
    N(
        booking: Skylane.Booking {
            refund: Skylane.Refund = booking S.booking Skylane.Refund
        }
    )
}
");
            var oldDescriptiveString = pipeline.ToOldDescriptiveString();
            oldDescriptiveString.Should().Be("S.airline F.type=\"Skylane.Airline.Day\" S.airlineDay F.type=\"Skylane.Flight\" E(S.flight F.type=\"Skylane.Flight.Cancellation\") S.flight F.type=\"Skylane.Booking\" N(S.booking F.type=\"Skylane.Refund\")");
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
            var pipeline = bookingsToRefund.Pipeline;
            var descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"airline: Skylane.Airline {
    flight: Skylane.Flight = airline S.airline Skylane.Airline.Day S.airlineDay Skylane.Flight
    E(
        flight: Skylane.Flight {
            cancellation: Skylane.Flight.Cancellation = flight S.flight Skylane.Flight.Cancellation
        }
    )
    booking: Skylane.Booking = flight S.flight Skylane.Booking
    N(
        booking: Skylane.Booking {
            refund: Skylane.Refund = booking S.booking Skylane.Refund
        }
    )
}
");
            var oldDescriptiveString = pipeline.ToOldDescriptiveString();
            oldDescriptiveString.Should().Be("S.airline F.type=\"Skylane.Airline.Day\" S.airlineDay F.type=\"Skylane.Flight\" E(S.flight F.type=\"Skylane.Flight.Cancellation\") S.flight F.type=\"Skylane.Booking\" N(S.booking F.type=\"Skylane.Refund\")");
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
            var pipeline = bookingsToRefund.Pipeline;
            var descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"airline: Skylane.Airline {
    flight: Skylane.Flight = airline S.airline Skylane.Airline.Day S.airlineDay Skylane.Flight
    booking: Skylane.Booking = flight S.flight Skylane.Booking
    N(
        booking: Skylane.Booking {
            refund: Skylane.Refund = booking S.booking Skylane.Refund
        }
    )
    cancellation: Skylane.Flight.Cancellation = flight S.flight Skylane.Flight.Cancellation
}
");
        }

        [Fact]
        public void Specification_SelectPredecessor()
        {
            var passengersForAirline = Given<Flight>.Match((flight, facts) =>
                from booking in facts.OfType<Booking>()
                where booking.flight == flight
                select booking.passenger
            );

            var pipeline = passengersForAirline.Pipeline;
            var descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"flight: Skylane.Flight {
    booking: Skylane.Booking = flight S.flight Skylane.Booking
    passenger: Skylane.Passenger = booking P.passenger Skylane.Passenger
}
");
        }

        [Fact]
        public void Specification_JoinWithTwoConditionals()
        {
            var specification = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                where !office.IsClosed

                from headcount in facts.OfType<Headcount>()
                where headcount.office == office
                where headcount.IsCurrent

                select new
                {
                    office,
                    headcount
                }
            );

            var pipeline = specification.Pipeline;
            var descriptiveString = pipeline.ToDescriptiveString();
            descriptiveString.Should().Be(@"company: Corporate.Company {
    office: Corporate.Office = company S.company Corporate.Office
    N(
        office: Corporate.Office {
            closure: Corporate.Office.Closure = office S.office Corporate.Office.Closure
        }
    )
    headcount: Corporate.Headcount = office S.office Corporate.Headcount
    N(
        headcount: Corporate.Headcount {
            next: Corporate.Headcount = headcount S.prior Corporate.Headcount
        }
    )
}
");
        }
    }
}
