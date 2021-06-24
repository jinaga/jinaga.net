using System;

namespace Jinaga.Test.Model
{
    [FactType("Skylane.Flight.Cancellation")]
    public record FlightCancellation(Flight flight, DateTime dateCancelled)
    {
    }
}