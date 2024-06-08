using System.Linq;

namespace Jinaga.Test.Model
{
    [FactType("Skylane.Airline")]
    public record Airline(string identifier)
    {
        public IQueryable<Flight> Flights => Relation.Define(facts =>
            from flight in facts.OfType<Flight>()
            where flight.airlineDay.airline == this &&
                !flight.IsCancelled
            select flight
        );
    }
}