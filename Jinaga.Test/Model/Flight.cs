using System.Linq;

namespace Jinaga.Test.Model
{
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

        public IQueryable<Booking> Bookings => Relation.Define(facts =>
            facts.OfType<Booking>(booking => booking.flight == this &&
                !facts.Any<Refund>(refund => refund.booking == booking)
            )
        );
    }
}