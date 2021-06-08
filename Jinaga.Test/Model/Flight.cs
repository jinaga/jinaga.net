using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Repository;

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
    }
}