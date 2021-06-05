using System;

namespace Jinaga.Test.Model
{
    [FactType("Skylane.Flight.Cancellation")]
    public class FlightCancellation
    {
        public Flight Flight { get; set; }
        public DateTime DateCancelled { get; set; }
    }
}