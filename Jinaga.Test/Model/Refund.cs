using System;

namespace Jinaga.Test.Model
{
    [FactType("Skylane.Refund")]
    public class Refund
    {
        public Booking Booking { get; set; }
        public DateTime DateRefunded { get; set; }
    }
}