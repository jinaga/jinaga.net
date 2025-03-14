using System.Linq;
using Jinaga.Extensions;
using Jinaga.Patterns;
using Jinaga.Test.Model;
using Jinaga.Test.Model.Order;

namespace Jinaga.Test.Specifications.Specifications
{
    public class SpecificationTest
    {
        [Fact]
        public void CanSpecifyIdentity()
        {
            Specification<Airline, Airline> specification = Given<Airline>.Select((airline, facts) => airline);
            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (airline: Skylane.Airline) {
                } => airline

                """
                );
        }

        [Fact]
        public void CanSpecifyIdentityWithShorthand()
        {
            Specification<Airline, Airline> specification = Given<Airline>.Match(airline => airline);
            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (airline: Skylane.Airline) {
                } => airline

                """
                );
        }

        [Fact]
        public void CanSpecifyIdentityTwoParameters()
        {
            Specification<Airline, User, User> specification = Given<Airline, User>.Select((airline, user, facts) => user);
            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (airline: Skylane.Airline, user: Jinaga.User) {
                } => user

                """
                );
        }

        [Fact]
        public void CanSpecifyIdentityTwoParametersWithShorthand()
        {
            Specification<Airline, User, User> specification = Given<Airline, User>.Match((airline, user) => user);
            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (airline: Skylane.Airline, user: Jinaga.User) {
                } => user

                """
                );
        }

        [Fact]
        public void CanSpecifySuccessors()
        {
            Specification<Airline, Flight> specification = Given<Airline>.Match((airline, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay.airline == airline
                select flight
            );
            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (airline: Skylane.Airline) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day->airline: Skylane.Airline = airline
                    ]
                } => flight

                """
                );
        }

        [Fact]
        public void CanSpecifySuccessorsWithSuccessorsExtension()
        {
            Specification<Airline, Flight> specification = Given<Airline>.Match(airline =>
                from flight in airline.Successors().OfType<Flight>(flight => flight.airlineDay.airline)
                select flight
            );
            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (airline: Skylane.Airline) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day->airline: Skylane.Airline = airline
                    ]
                } => flight

                """
                );
        }

        [Fact]
        public void CanSpecifySuccessorsThroughCollectionWithSuccessorsExtension()
        {
            var specification = Given<Airline>.Match((airline, facts) =>
                from flight in airline.Successors().OfType<Flight>(flight => flight.airlineDay.airline)
                from itinerary in flight.Successors().OfType<Itinerary>(itinerary => itinerary.flights)
                select itinerary
            );

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (airline: Skylane.Airline) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day->airline: Skylane.Airline = airline
                    ]
                    itinerary: Skylane.Itinerary [
                        itinerary->flights: Skylane.Flight = flight
                    ]
                } => itinerary

                """
                );
        }

        [Fact]
        public void CanSpecifyGiven()
        {
            Specification<Airline, Flight> specification = Given<Airline>.Match((airline, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay.airline == airline
                select flight
            );
            var airline = new Airline("Bazinga");
            specification.ToDescriptiveString(airline).ReplaceLineEndings().Should().Be(
                """
                let airline: Skylane.Airline = #GFroig3rCwOPo1yS0N68PUHIiD20s6H8L0uResV0BNdt+sr40nogatku8z+zeBwvqYoWvTS9sMl1SBCpVZ5MtA==

                (airline: Skylane.Airline) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day->airline: Skylane.Airline = airline
                    ]
                } => flight

                """
                );
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
            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (airline: Skylane.Airline) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day->airline: Skylane.Airline = airline
                    ]
                } => flight

                """
                );
        }

        [Fact]
        public void CanSpecifyPredecessors()
        {
            Specification<FlightCancellation, Flight> specification = Given<FlightCancellation>.Match((flightCancellation, facts) =>
                from flight in facts.OfType<Flight>()
                where flightCancellation.flight == flight
                select flight
            );
            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (flightCancellation: Skylane.Flight.Cancellation) {
                    flight: Skylane.Flight [
                        flight = flightCancellation->flight: Skylane.Flight
                    ]
                } => flight

                """
                );
        }

        [Fact]
        public void CanSpecifyPredecessorsShorthand()
        {
            Specification<FlightCancellation, Flight> specification = Given<FlightCancellation>.Match(flightCancellation =>
                flightCancellation.flight
            );
            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (flightCancellation: Skylane.Flight.Cancellation) {
                    flight: Skylane.Flight [
                        flight = flightCancellation->flight: Skylane.Flight
                    ]
                } => flight

                """
                );
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
            activeFlights.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
        }

        [Fact]
        public void CanSpecifyNegativeExistentialConditionWithSuccessorsExtension()
        {
            Specification<AirlineDay, Flight> activeFlights = Given<AirlineDay>.Match(airlineDay =>
                from flight in airlineDay.Successors().OfType<Flight>(flight => flight.airlineDay)
                where flight.Successors().No<FlightCancellation>(cancellation => cancellation.flight)
                select flight
            );
            activeFlights.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
        }

        [Fact]
        public void CanSpecifyMultipleNegativeExistentialConditionsWithSameLambdaParameterName()
        {
            var bookingsForAirlineDay = Given<AirlineDay>.Match(airlineDay =>
                airlineDay.Successors().OfType<Flight>(flight => flight.airlineDay)
                    .WhereNo((FlightCancellation x) => x.flight)
                    .SelectMany(flight => flight.Successors().OfType<Booking>(booking => booking.flight))
                    .WhereNo((Refund x) => x.booking)
            );

            bookingsForAirlineDay.ToString().ReplaceLineEndings().Should().Be(
                """
                (airlineDay: Skylane.Airline.Day) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day = airlineDay
                        !E {
                            x: Skylane.Flight.Cancellation [
                                x->flight: Skylane.Flight = flight
                            ]
                        }
                    ]
                    booking: Skylane.Booking [
                        booking->flight: Skylane.Flight = flight
                        !E {
                            x2: Skylane.Refund [
                                x2->booking: Skylane.Booking = booking
                            ]
                        }
                    ]
                } => booking

                """
                );
        }

        [Fact]
        public void CanSpecifyNegativeExistentialConditionWithWhereNo()
        {
            Specification<AirlineDay, Flight> activeFlights = Given<AirlineDay>.Match((airlineDay, facts) =>
                airlineDay.Successors().OfType<Flight>(flight => flight.airlineDay)
                    .WhereNo((FlightCancellation cancellation) => cancellation.flight)
            );
            activeFlights.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
        }

        [Fact]
        public void CanPassPredicateToAny()
        {
            var shortSpecification = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay == airlineDay

                where !facts.OfType<FlightCancellation>().Any(cancellation => cancellation.flight == flight)

                select flight
            );
            var longSpecification = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay == airlineDay

                where !(
                    from cancellation in facts.OfType<FlightCancellation>()
                    where cancellation.flight == flight
                    select cancellation
                ).Any()

                select flight
            );

            shortSpecification.ToString().Should().Be(longSpecification.ToString());
        }

        [Fact]
        public void CanCombinePredicatesWithAndOperator()
        {
            var conjunctionSpecification = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay == airlineDay &&
                    !facts.OfType<FlightCancellation>().Any(cancellation => cancellation.flight == flight)
                select flight
            );
            var longSpecification = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay == airlineDay

                where !(
                    from cancellation in facts.OfType<FlightCancellation>()
                    where cancellation.flight == flight
                    select cancellation
                ).Any()

                select flight
            );

            conjunctionSpecification.ToString().Should().Be(longSpecification.ToString());
        }

        [Fact]
        public void CanPassConditionToFactsAny()
        {
            var shorterSpecification = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay == airlineDay &&
                    !facts.Any<FlightCancellation>(cancellation => cancellation.flight == flight)
                select flight
            );
            var longSpecification = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay == airlineDay

                where !(
                    from cancellation in facts.OfType<FlightCancellation>()
                    where cancellation.flight == flight
                    select cancellation
                ).Any()

                select flight
            );

            shorterSpecification.ToString().Should().Be(longSpecification.ToString());
        }

        [Fact]
        public void CanProcessTheShortestPossibleSpecification()
        {
            var shortestSpecification = Given<AirlineDay>.Match((airlineDay, facts) =>
                facts.OfType<Flight>(flight =>
                    flight.airlineDay == airlineDay &&
                    !facts.Any<FlightCancellation>(cancellation =>
                        cancellation.flight == flight
                    )
                )
            );
            var longSpecification = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay == airlineDay

                where !(
                    from cancellation in facts.OfType<FlightCancellation>()
                    where cancellation.flight == flight
                    select cancellation
                ).Any()

                select flight
            );

            shortestSpecification.ToString().Should().Be(longSpecification.ToString());
        }

        [Fact]
        public void CanProcessShortSpecificationWithSuccessorExtension()
        {
            var shortestSpecification = Given<AirlineDay>.Match(airlineDay =>
                airlineDay.Successors().OfType<Flight>(flight => flight.airlineDay)
                    .Where(flight =>
                        flight.Successors().No<FlightCancellation>(cancellation =>
                            cancellation.flight
                        )
                    )
            );
            var longSpecification = Given<AirlineDay>.Match((airlineDay, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay == airlineDay

                where !(
                    from cancellation in facts.OfType<FlightCancellation>()
                    where cancellation.flight == flight
                    select cancellation
                ).Any()

                select flight
            );

            shortestSpecification.ToString().Should().Be(longSpecification.ToString());
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
            activeFlights.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
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
            activeFlights.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
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
            bookingsToRefund.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
        }

        [Fact]
        public void CanSpecifyPositiveExistentialConditionWithSuccessorExtension()
        {
            Specification<Airline, Booking> bookingsToRefund = Given<Airline>.Match(airline =>
                from flight in airline.Successors().OfType<Flight>(flight => flight.airlineDay.airline)
                where flight.Successors().Any<FlightCancellation>(cancellation => cancellation.flight)
                from booking in flight.Successors().OfType<Booking>(booking => booking.flight)
                where booking.Successors().No<Refund>(refund => refund.booking)
                select booking
            );
            bookingsToRefund.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
        }

        [Fact]
        public void CanSpecifyPositiveExistentialConditionWithWhereAny()
        {
            Specification<Airline, Booking> bookingsToRefund = Given<Airline>.Match(airline =>
                airline.Successors().OfType<Flight>(flight => flight.airlineDay.airline)
                    .WhereAny((FlightCancellation cancellation) => cancellation.flight)
                    .SelectMany(flight => flight.Successors().OfType<Booking>(booking => booking.flight))
                    .WhereNo((Refund refund) => refund.booking)
            );

            bookingsToRefund.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
        }

        [Fact]
        public void CanSpecifyPositiveExistentialConditionThroughCollectionWithSuccessorExtension()
        {
            var flightsHavingItineraries = Given<Airline>.Match(airline =>
                from flight in airline.Successors().OfType<Flight>(flight => flight.airlineDay.airline)
                where flight.Successors().Any<Itinerary>(itinerary => itinerary.flights)
                select flight
            );

            flightsHavingItineraries.ToString().ReplaceLineEndings().Should().Be(
                """
                (airline: Skylane.Airline) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day->airline: Skylane.Airline = airline
                        E {
                            itinerary: Skylane.Itinerary [
                                itinerary->flights: Skylane.Flight = flight
                            ]
                        }
                    ]
                } => flight

                """
                );
        }

        [Fact]
        public void CanSpecifyPositiveExistentialConditionThroughCollectionWithWhereAny()
        {
            var flightsHavingItineraries = Given<Airline>.Match(airline =>
                airline.Successors().OfType<Flight>(flight => flight.airlineDay.airline)
                    .WhereAny((Itinerary itinerary) => itinerary.flights)
            );

            flightsHavingItineraries.ToString().ReplaceLineEndings().Should().Be(
                """
                (airline: Skylane.Airline) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day->airline: Skylane.Airline = airline
                        E {
                            itinerary: Skylane.Itinerary [
                                itinerary->flights: Skylane.Flight = flight
                            ]
                        }
                    ]
                } => flight

                """
                );
        }

        [Fact]
        public void CanCallSelectManyWithOneParameter()
        {
            var testSpecification = Given<Airline>.Match((airline, facts) =>
                facts.OfType<Flight>(flight =>
                    flight.airlineDay.airline == airline &&
                    facts.Any<FlightCancellation>(cancellation =>
                        cancellation.flight == flight
                    )
                )
                .SelectMany(flight => facts.OfType<Booking>(booking =>
                    booking.flight == flight &&
                    !facts.Any<Refund>(refund =>
                        refund.booking == booking
                    )
                ))
            );
            var referenceSpecification = Given<Airline>.Match((airline, facts) =>
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

            testSpecification.ToString().Should().Be(referenceSpecification.ToString());
        }

        [Fact]
        public void CanJoinViaRelation()
        {
            var testSpecification = Given<Airline>.Match((airline, facts) =>
                from flight in airline.Flights
                from booking in flight.Bookings
                select booking
            );

            var referenceSpecification = Given<Airline>.Match((airline, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.airlineDay.airline == airline

                where !(
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

            testSpecification.ToString().Should().Be(referenceSpecification.ToString());
        }

        [Fact]
        public void CanPutWhereAfterSelectMany()
        {
            var testSpecification = Given<Airline>.Match((airline, facts) =>
                facts.OfType<Flight>(flight =>
                    flight.airlineDay.airline == airline &&
                    facts.Any<FlightCancellation>(cancellation =>
                        cancellation.flight == flight
                    )
                )
                .SelectMany(flight => facts.OfType<Booking>(booking =>
                    booking.flight == flight))
                .Where(booking =>
                    !facts.Any<Refund>(refund =>
                        refund.booking == booking
                    )
                )
            );
            var referenceSpecification = Given<Airline>.Match((airline, facts) =>
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

            testSpecification.ToString().Should().Be(referenceSpecification.ToString());
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
            bookingsToRefund.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
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
            bookingsToRefund.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
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

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
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

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
        }

        [Fact]
        public void Specification_PropertyPatternExtension()
        {
            var specification = Given<Company>.Match(company =>
                company.Successors().OfType<Office>(office => office.company)
                    .Where(office => !office.IsClosed)
                    .Select(office => new
                    {
                        office,
                        headcount = office.Successors().OfType<Headcount>(headcount => headcount.office)
                            .WhereCurrent(next => next.prior)
                            .Select(headcount => headcount.value)
                    })
                );

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
        }

        [Fact]
        public void Specification_RelationInProjection()
        {
            var specification = Given<Company>.Match((company, facts) =>
                company.Offices.Select(office =>
                    new
                    {
                        office,
                        headcount = office.Headcount.Select(headcount => headcount.value)
                    }
                )
            );

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
        }

        [Fact]
        public void Specification_ProjectionsFromIdentity()
        {
            var specification = Given<Office>.Select((office, facts) =>
                new
                {
                    Managers = office.Managers,
                    Headcount = office.Headcount
                });

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (office: Corporate.Office) {
                } => {
                    Headcount = {
                        headcount: Corporate.Headcount [
                            headcount->office: Corporate.Office = office
                            !E {
                                next: Corporate.Headcount [
                                    next->prior: Corporate.Headcount = headcount
                                ]
                            }
                        ]
                    } => headcount
                    Managers = {
                        manager: Corporate.Manager [
                            manager->office: Corporate.Office = office
                            !E {
                                termination: Corporate.Manager.Terminated [
                                    termination->Manager: Corporate.Manager = manager
                                ]
                            }
                        ]
                    } => manager
                }

                """
                );
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

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
        }

        [Fact]
        public void Specification_DeeplyNestedRelations()
        {
            var specification = Given<Company>.Match((company, facts) =>
                company.Offices.Select(office =>
                    new
                    {
                        office,
                        managers = office.Managers.Select(manager =>
                            new
                            {
                                manager,
                                name = manager.Names.Select(name => name.value)
                            }
                        )
                    }
                )
            );

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
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
                                termination: Corporate.Manager.Terminated [
                                    termination->Manager: Corporate.Manager = manager
                                ]
                            }
                        ]
                    } => {
                        manager = manager
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

                """
                );
        }

        [Fact]
        public void Specification_SimpleValueRelation()
        {
            var specification = Given<Manager>.Match((manager, facts) =>
                manager.NameValues
            );

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (manager: Corporate.Manager) {
                    name: Corporate.Manager.Name [
                        name->manager: Corporate.Manager = manager
                        !E {
                            next: Corporate.Manager.Name [
                                next->prior: Corporate.Manager.Name = name
                            ]
                        }
                    ]
                } => name.value

                """
                );
        }

        [Fact]
        public void Specification_RelationsSelectingValues()
        {
            var specification = Given<Company>.Match((company, facts) =>
                company.Offices.Select(office =>
                    new
                    {
                        office,
                        managers = office.Managers.Select(manager =>
                            new
                            {
                                manager,
                                name = manager.NameValues
                            }
                        )
                    }
                )
            );

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
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
                                termination: Corporate.Manager.Terminated [
                                    termination->Manager: Corporate.Manager = manager
                                ]
                            }
                        ]
                    } => {
                        manager = manager
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

                """
                );
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

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
        }

        [Fact]
        public void Specification_RestorePatternExtension()
        {
            var specification = Given<Company>.Match(company =>
                company.Successors().OfType<Office>(office => office.company)
                    .WhereNotDeletedOrRestored(
                        (OfficeClosure officeClosure) => officeClosure.office,
                        (OfficeReopening officeReopening) => officeReopening.officeClosure
                    ));

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
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

                """
                );
        }

        [Fact]
        public void CanSpecifyNestedExistentialConditions()
        {
            var specification = Given<Department>.Match((department, facts) =>
                facts.OfType<Project>()
                    .Where(project => project.department == department)
                    .Where(project => !facts.OfType<ProjectDeleted>()
                        .Where(deleted => deleted.project == project)
                        .Where(deleted => !facts.OfType<ProjectRestored>()
                            .Where(restored => restored.deleted == deleted)
                            .Any()
                        )
                        .Any()
                    )
            );
            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (department: Department) {
                    project: Project [
                        project->department: Department = department
                        !E {
                            deleted: Project.Deleted [
                                deleted->project: Project = project
                                !E {
                                    restored: Project.Restored [
                                        restored->deleted: Project.Deleted = deleted
                                    ]
                                }
                            ]
                        }
                    ]
                } => project
                
                """
            );
        }

        [Fact]
        public void CanSpecifyOtherNestedExistentialCondition()
        {
            var specification = Given<School>.Match((school, facts) =>
              from course in facts.OfType<Course>()
              where course.school == school
              where !(
                from deleted in facts.OfType<CourseDeleted>()
                where deleted.course == course
                where !(
                  from restored in facts.OfType<CourseRestored>()
                  where restored.deleted == deleted
                  select restored
                ).Any()
                select deleted
              ).Any()
              select course
            );
            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (school: School) {
                    course: Course [
                        course->school: School = school
                        !E {
                            deleted: Course.Deleted [
                                deleted->course: Course = course
                                !E {
                                    restored: Course.Restored [
                                        restored->deleted: Course.Deleted = deleted
                                    ]
                                }
                            ]
                        }
                    ]
                } => course
                
                """
            );
        }

        [Fact]
        public void CanUseShortNameForJoinExpression()
        {
            var specification = Given<Company>.Match(company =>
                from office in company.Successors().OfType<Office>(o => o.company)
                select office
            );

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (company: Corporate.Company) {
                    office: Corporate.Office [
                        office->company: Corporate.Company = company
                    ]
                } => office

                """
            );
        }

        [Fact]
        public void CanUseDuplicateNamesInLambdas()
        {
            var specification = Given<Company>.Match(company =>
                company.Successors().OfType<Office>(o => o.company)
                    .SelectMany(f => f.Successors().OfType<Manager>(f => f.office))
            );

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (company: Corporate.Company) {
                    f: Corporate.Office [
                        f->company: Corporate.Company = company
                    ]
                    f2: Corporate.Manager [
                        f2->office: Corporate.Office = f
                    ]
                } => f2

                """
            );
        }

        [Fact]
        public void CanReuseGivenNameInLambdas()
        {
            var specification = Given<Company>.Match(company =>
                company.Successors().OfType<Office>(office => office.company)
                    .SelectMany(company => company.Successors().OfType<Manager>(manager => manager.office))
            );

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (company: Corporate.Company) {
                    company2: Corporate.Office [
                        company2->company: Corporate.Company = company
                    ]
                    manager: Corporate.Manager [
                        manager->office: Corporate.Office = company2
                    ]
                } => manager

                """
            );
        }

        [Fact]
        public void CanReuseALabelInAnExistentialCondition()
        {
            var specification = Given<Company>.Match(closure =>
                closure.Successors().OfType<Office>(office => office.company)
                    .Where(office => !office.IsClosed)
                    .SelectMany(office => office.Successors().OfType<Manager>(manager => manager.office))
            );

            specification.ToString().ReplaceLineEndings().Should().Be(
                """
                (closure: Corporate.Company) {
                    office: Corporate.Office [
                        office->company: Corporate.Company = closure
                        !E {
                            closure2: Corporate.Office.Closure [
                                closure2->office: Corporate.Office = office
                            ]
                        }
                    ]
                    manager: Corporate.Manager [
                        manager->office: Corporate.Office = office
                    ]
                } => manager

                """
            );

            var inverses = specification.ComputeInverses();
            inverses.Select(i => i.InverseSpecification.ToString().ReplaceLineEndings()).Should().BeEquivalentTo(
                [
                    """
                    (manager: Corporate.Manager) {
                        office: Corporate.Office [
                            office = manager->office: Corporate.Office
                            !E {
                                closure2: Corporate.Office.Closure [
                                    closure2->office: Corporate.Office = office
                                ]
                            }
                        ]
                        closure: Corporate.Company [
                            closure = office->company: Corporate.Company
                        ]
                    } => manager

                    """,
                    // This is the reason we cannot redefine the label in the existential condition.
                    """
                    (closure2: Corporate.Office.Closure) {
                        office: Corporate.Office [
                            office = closure2->office: Corporate.Office
                        ]
                        closure: Corporate.Company [
                            closure = office->company: Corporate.Company
                        ]
                        manager: Corporate.Manager [
                            manager->office: Corporate.Office = office
                        ]
                    } => manager

                    """
                ]
            );
        }
    }
}
