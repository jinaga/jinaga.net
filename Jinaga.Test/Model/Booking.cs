using System;

namespace Jinaga.Test.Model
{
    [FactType("Skylane.Booking")]
    public record Booking(Flight flight, Passenger passenger, DateTime dateBooked)
    {
    }
}