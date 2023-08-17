namespace Jinaga.Store.SQLite.Test.Models;


[FactType("Skylane.Airline")]
internal record Airline(string identifier);



[FactType("Skylane.Airline.Day")]
internal record AirlineDay(Airline airline, DateTime date);


[FactType("Skylane.Flight")]
internal record Flight(AirlineDay airlineDay, int flightNumber)
{
    public Condition IsCancelled => new Condition(facts =>
        (
            from cancellation in facts.OfType<FlightCancellation>()
            where cancellation.flight == this
            select cancellation
        ).Any()
    );

    public Condition ShortIsCancelled => new Condition(facts =>
        facts.OfType<FlightCancellation>(cancellation => cancellation.flight == this).Any()
    );
}


[FactType("Skylane.Flight.Cancellation")]
internal record FlightCancellation(Flight flight, DateTime dateCancelled);


[FactType("Skylane.Passenger")]
internal record Passenger(Airline airline, User user);


[FactType("Skylane.Passenger.Name")]
internal record PassengerName(Passenger passenger, string value, PassengerName[] prior);


[FactType("Skylane.Booking")]
internal record Booking(Flight flight, Passenger passenger, DateTime dateBooked);


[FactType("Skylane.Refund")]
internal record Refund(Booking booking, DateTime dateRefunded);
