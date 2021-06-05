using System;

namespace Jinaga.Test.Model
{
    [FactType("Skylane.Booking")]
    public class Booking
    {
        public Flight Flight { get; set; }
        public Passenger Passenger { get; set; }
        public DateTime DateBooked { get; set; }
    }
}