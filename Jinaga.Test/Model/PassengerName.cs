namespace Jinaga.Test.Model
{
    [FactType("Skylane.Passenger.Name")]
    public record PassengerName(Passenger passenger, string value, PassengerName[] prior);
}