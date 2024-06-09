namespace Jinaga.Store.SQLite.Test.Models;


[FactType("Skylane.Airline")]
public record Airline(string identifier);



[FactType("Skylane.Airline.Day")]
public record AirlineDay(Airline airline, DateTime date);


[FactType("Skylane.Flight")]
public record Flight(AirlineDay airlineDay, int flightNumber)
{
    public Condition IsCancelled => Condition.Define(facts =>
        (
            from cancellation in facts.OfType<FlightCancellation>()
            where cancellation.flight == this
            select cancellation
        ).Any()
    );

    public Condition ShortIsCancelled => Condition.Define(facts =>
        facts.OfType<FlightCancellation>(cancellation => cancellation.flight == this).Any()
    );
}


[FactType("Skylane.Flight.Cancellation")]
public record FlightCancellation(Flight flight, DateTime dateCancelled);


[FactType("Skylane.Passenger")]
public record Passenger(Airline airline, User user);


[FactType("Skylane.Passenger.Name")]
public record PassengerName(Passenger passenger, string value, PassengerName[] prior);


[FactType("Skylane.Booking")]
public record Booking(Flight flight, Passenger passenger, DateTime dateBooked);


[FactType("Skylane.Refund")]
public record Refund(Booking booking, DateTime dateRefunded);
