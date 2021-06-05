using System;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Repository;

namespace Jinaga.Test.Model
{
    [FactType("Skylane.Flight")]
    public class Flight
    {
        public AirlineDay AirlineDay { get; set; }
        public int FlightNumber { get; set; }

        public Expression<Func<FactRepository, bool>> IsCancelled => (FactRepository facts) =>
            facts.Some(
                from cancellation in facts.OfType<FlightCancellation>()
                where cancellation.Flight == this
                select cancellation
            );
    }
}