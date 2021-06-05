namespace Jinaga.Test.Model
{
    [FactType("Skylane.Passenger")]
    public record Passenger(Airline airline, User user)
    {
    }
}