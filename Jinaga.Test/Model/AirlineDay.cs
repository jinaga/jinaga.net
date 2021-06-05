using System;

namespace Jinaga.Test.Model
{
    [FactType("Skyline.Airline.Day")]
    public record AirlineDay(Airline airline, DateTime date)
    {
    }
}