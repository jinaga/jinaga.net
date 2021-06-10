using System;

namespace Jinaga.Test.Model
{
    [FactType("Skylane.Airline.Day")]
    public record AirlineDay(Airline airline, DateTime date)
    {
    }
}