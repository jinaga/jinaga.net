namespace Jinaga.FSharp.Test.Model

open System
open Jinaga

[<FactType("Skylane.Airline")>]
type public Airline = {
    Identifier: string
}

and [<FactType("Skylane.Airline.Day")>] public AirlineDay = {
    airline: Airline
    date: DateTime
}

and [<FactType("Skylane.Passenger")>] public Passenger = {
    airline: Airline
    user: User
}

and [<FactType("Skylane.Passenger.Name")>] public PassengerName = {
    passenger: Passenger
    value: string
    prior: PassengerName list
}

and [<FactType("Skylane.Flight")>] public Flight = {
    AirlineDay: AirlineDay
    FlightNumber: int
}

and [<FactType("Skylane.Flight.Cancellation")>] public FlightCancellation = {
    flight: Flight
    dateCancelled: DateTime
}

and [<FactType("Skylane.Booking")>] public Booking = {
    flight: Flight
    passenger: Passenger
    dateBooked: DateTime
}

and [<FactType("Skylane.Refund")>] public Refund = {
    booking: Booking
    dateRefunded: DateTime
}