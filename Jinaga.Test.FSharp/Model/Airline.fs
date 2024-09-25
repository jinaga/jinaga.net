namespace Jinaga.FSharp.Test.Model

open System
open Jinaga

[<FactType("Skylane.Airline")>]
type Airline = {
    Identifier: string
}

and [<FactType("Skylane.Airline.Day")>] AirlineDay = {
    airline: Airline
    date: DateTime
}

and [<FactType("Skylane.Passenger")>] Passenger = {
    airline: Airline
    user: User
}

and [<FactType("Skylane.Passenger.Name")>] PassengerName = {
    passenger: Passenger
    value: string
    prior: PassengerName list
}

and [<FactType("Skylane.Flight")>]  Flight = {
    AirlineDay: AirlineDay
    FlightNumber: int
}

and [<FactType("Skylane.Flight.Cancellation")>] FlightCancellation = {
    flight: Flight
    dateCancelled: DateTime
}

and [<FactType("Skylane.Booking")>] Booking = {
    flight: Flight
    passenger: Passenger
    dateBooked: DateTime
}

and [<FactType("Skylane.Refund")>] Refund = {
    booking: Booking
    dateRefunded: DateTime
}