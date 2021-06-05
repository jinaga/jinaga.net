using System;

namespace Jinaga.Test.Model
{
    [FactType("Skyline.Airline.Day")]
    public class AirlineDay
    {
        public Airline Airline { get; set; }
        public DateTime Date { get; set; }
    }
}