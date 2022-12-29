using System;
using System.Linq;
using FluentAssertions;
using Jinaga.Test.Model;
using Jinaga.Test.Model.Order;
using Xunit;
using static Jinaga.Test.Helpers;

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
            string descriptiveString = specification.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (airline: Skylane.Airline) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day->airline: Skylane.Airline = airline
                    ]
                } => flight
                "));
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
                    "The variable \"flight\" should be joined to the parameter \"airline\".");
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
                    "The variable \"order\" should be joined to the parameter \"item\"."
                );
        }

        [Fact]
        public void Specification_MissingCollectionJoinWithExtension()
        {
            Func<Specification<Item, Order>> constructor = () =>
                Given<Item>.Match((item, facts) =>
                    facts.OfType<Order>()
                );
            constructor.Should().Throw<SpecificationException>()
                .WithMessage(
                    "The variable \"unknown\" should be joined to the parameter \"item\"."
                );
        }

        [Fact]
        public void Specification_MissingSuccessorCollectionJoin()
        {
            Func<Specification<Order, Item>> constructor = () =>
                Given<Order>.Match((order, facts) =>
                    from item in facts.OfType<Item>()
                    select item
                );
            constructor.Should().Throw<SpecificationException>()
                .WithMessage(
                    "The variable \"item\" should be joined to the parameter \"order\"."
                );
        }

        [Fact]
        public void Specification_MissingSuccessorCollectionJoinWithExtension()
        {
            Func<Specification<Order, Item>> constructor = () =>
                Given<Order>.Match((order, facts) =>
                    facts.OfType<Item>()
                );
            constructor.Should().Throw<SpecificationException>()
                .WithMessage(
                    "The variable \"unknown\" should be joined to the parameter \"order\"."
                );
        }

        [Fact]
        public void Specification_MissingJoinWithExtensionMethod()
        {
            Func<Specification<Airline, Flight>> constructor = () =>
                Given<Airline>.Match((airline, facts) => facts.OfType<Flight>());
            constructor.Should().Throw<SpecificationException>()
                .WithMessage(
                    "The variable \"unknown\" should be joined to the parameter \"airline\".");
        }

        [Fact]
        public void CanSpecifyShortSuccessors()
        {
            Specification<Airline, Flight> specification = Given<Airline>.Match((airline, facts) =>
                facts.OfType<Flight>(flight => flight.airlineDay.airline == airline)
            );
            string descriptiveString = specification.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (airline: Skylane.Airline) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day->airline: Skylane.Airline = airline
                    ]
                } => flight
                "));
        }

        [Fact]
        public void CanSpecifyPredecessors()
        {
            Specification<FlightCancellation, Flight> specification = Given<FlightCancellation>.Match((flightCancellation, facts) =>
                from flight in facts.OfType<Flight>()
                where flightCancellation.flight == flight
                select flight
            );
            string descriptiveString = specification.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (flightCancellation: Skylane.Flight.Cancellation) {
                    flight: Skylane.Flight [
                        flight = flightCancellation->flight: Skylane.Flight
                    ]
                } => flight
                "));
        }

        [Fact]
        public void CanSpecifyPredecessorsShorthand()
        {
            Specification<FlightCancellation, Flight> specification = Given<FlightCancellation>.Match(flightCancellation =>
                flightCancellation.flight
            );
            string descriptiveString = specification.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (flightCancellation: Skylane.Flight.Cancellation) {
                    flight: Skylane.Flight [
                        flight = flightCancellation->flight: Skylane.Flight
                    ]
                } => flight
                "));
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
            var descriptiveString = activeFlights.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (airlineDay: Skylane.Airline.Day) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day = airlineDay
                        !E {
                            cancellation: Skylane.Flight.Cancellation [
                                cancellation->flight: Skylane.Flight = flight
                            ]
                        }
                    ]
                } => flight
                "));
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
            var descriptiveString = activeFlights.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (airlineDay: Skylane.Airline.Day) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day = airlineDay
                        !E {
                            cancellation: Skylane.Flight.Cancellation [
                                cancellation->flight: Skylane.Flight = flight
                            ]
                        }
                    ]
                } => flight
                "));
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
            var descriptiveString = activeFlights.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (airlineDay: Skylane.Airline.Day) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day = airlineDay
                        !E {
                            cancellation: Skylane.Flight.Cancellation [
                                cancellation->flight: Skylane.Flight = flight
                            ]
                        }
                    ]
                } => flight
                "));
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
            var descriptiveString = bookingsToRefund.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (airline: Skylane.Airline) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day->airline: Skylane.Airline = airline
                        E {
                            cancellation: Skylane.Flight.Cancellation [
                                cancellation->flight: Skylane.Flight = flight
                            ]
                        }
                    ]
                    booking: Skylane.Booking [
                        booking->flight: Skylane.Flight = flight
                        !E {
                            refund: Skylane.Refund [
                                refund->booking: Skylane.Booking = booking
                            ]
                        }
                    ]
                } => booking
                "));
        }

        [Fact]
        public void Specification_MissingJoinToPriorPath()
        {
            Func<Specification<Airline, Booking>> bookingsToRefund = () => Given<Airline>.Match((airline, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay.airline == airline
                from booking in facts.OfType<Booking>()
                select booking
            );

            bookingsToRefund.Should().Throw<SpecificationException>().WithMessage(
                "The variable \"booking\" should be joined to the prior variable \"flight\"."
            );
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
            var descriptiveString = bookingsToRefund.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (airline: Skylane.Airline) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day->airline: Skylane.Airline = airline
                        E {
                            cancellation: Skylane.Flight.Cancellation [
                                cancellation->flight: Skylane.Flight = flight
                            ]
                        }
                    ]
                    booking: Skylane.Booking [
                        booking->flight: Skylane.Flight = flight
                        !E {
                            refund: Skylane.Refund [
                                refund->booking: Skylane.Booking = booking
                            ]
                        }
                    ]
                } => booking
                "));
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
            var descriptiveString = bookingsToRefund.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (airline: Skylane.Airline) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day->airline: Skylane.Airline = airline
                    ]
                    cancellation: Skylane.Flight.Cancellation [
                        cancellation->flight: Skylane.Flight = flight
                    ]
                    booking: Skylane.Booking [
                        booking->flight: Skylane.Flight = flight
                        !E {
                            refund: Skylane.Refund [
                                refund->booking: Skylane.Booking = booking
                            ]
                        }
                    ]
                } => {
                    Booking = booking
                    Cancellation = cancellation
                }
                "));
        }

        [Fact]
        public void Specification_SelectPredecessor()
        {
            Func<Specification<Flight, Passenger>> passengersForAirline = () => Given<Flight>.Match((flight, facts) =>
                from booking in facts.OfType<Booking>()
                where booking.flight == flight
                select booking.passenger
            );

            passengersForAirline.Should().Throw<SpecificationException>().WithMessage(
                "Cannot select passenger directly. Give the fact a label first."
            );
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

            var descriptiveString = specification.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (company: Corporate.Company) {
                    office: Corporate.Office [
                        office->company: Corporate.Company = company
                        !E {
                            closure: Corporate.Office.Closure [
                                closure->office: Corporate.Office = office
                            ]
                        }
                    ]
                    headcount: Corporate.Headcount [
                        headcount->office: Corporate.Office = office
                        !E {
                            next: Corporate.Headcount [
                                next->prior: Corporate.Headcount = headcount
                            ]
                        }
                    ]
                } => {
                    headcount = headcount
                    office = office
                }
                "));
        }

        [Fact]
        public void Specification_NestedProjection()
        {
            var specification = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                where !office.IsClosed

                select new
                {
                    office,
                    headcount =
                        from headcount in facts.OfType<Headcount>()
                        where headcount.office == office
                        where headcount.IsCurrent
                        select headcount.value
                }
            );

            var descriptiveString = specification.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (company: Corporate.Company) {
                    office: Corporate.Office [
                        office->company: Corporate.Company = company
                        !E {
                            closure: Corporate.Office.Closure [
                                closure->office: Corporate.Office = office
                            ]
                        }
                    ]
                } => {
                    headcount = {
                        headcount: Corporate.Headcount [
                            headcount->office: Corporate.Office = office
                            !E {
                                next: Corporate.Headcount [
                                    next->prior: Corporate.Headcount = headcount
                                ]
                            }
                        ]
                    } => headcount.value
                    office = office
                }
                "));
        }

        [Fact]
        public void Specification_DeeplyNestedProjection()
        {
            var specification = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                where !office.IsClosed

                select new
                {
                    office,
                    managers =
                        from manager in facts.OfType<Manager>()
                        where manager.office == office
                        where !(
                            from terminated in facts.OfType<ManagerTerminated>()
                            where terminated.Manager == manager
                            select terminated
                        ).Any()
                        select new
                        {
                            name =
                                from name in facts.OfType<ManagerName>()
                                where name.manager == manager
                                where name.IsCurrent
                                select name.value
                        }
                }
            );

            var descriptiveString = specification.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (company: Corporate.Company) {
                    office: Corporate.Office [
                        office->company: Corporate.Company = company
                        !E {
                            closure: Corporate.Office.Closure [
                                closure->office: Corporate.Office = office
                            ]
                        }
                    ]
                } => {
                    managers = {
                        manager: Corporate.Manager [
                            manager->office: Corporate.Office = office
                            !E {
                                terminated: Corporate.Manager.Terminated [
                                    terminated->Manager: Corporate.Manager = manager
                                ]
                            }
                        ]
                    } => {
                        name = {
                            name: Corporate.Manager.Name [
                                name->manager: Corporate.Manager = manager
                                !E {
                                    next: Corporate.Manager.Name [
                                        next->prior: Corporate.Manager.Name = name
                                    ]
                                }
                            ]
                        } => name.value
                    }
                    office = office
                }
                "));
        }

        [Fact]
        public void Specification_RestorePattern()
        {
            var specification = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                where !(
                    from officeClosure in facts.OfType<OfficeClosure>()
                    where officeClosure.office == office
                    where !(
                        from officeReopening in facts.OfType<OfficeReopening>()
                        where officeReopening.officeClosure == officeClosure
                        select officeReopening
                    ).Any()
                    select officeClosure
                ).Any()
                select office
            );

            var descriptiveString = specification.ToDescriptiveString(4);
            descriptiveString.Should().Be(Indented(4, @"
                (company: Corporate.Company) {
                    office: Corporate.Office [
                        office->company: Corporate.Company = company
                        !E {
                            officeClosure: Corporate.Office.Closure [
                                officeClosure->office: Corporate.Office = office
                                !E {
                                    officeReopening: Corporate.Office.Reopening [
                                        officeReopening->officeClosure: Corporate.Office.Closure = officeClosure
                                    ]
                                }
                            ]
                        }
                    ]
                } => office
                "));
        }
    }
}
