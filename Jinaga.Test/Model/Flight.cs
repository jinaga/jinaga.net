using System.Linq;

namespace Jinaga.Test.Model
{
    [FactType("Skylane.Flight")]
    public record Flight(AirlineDay airlineDay, int flightNumber)
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
}