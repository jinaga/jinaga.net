using System;

namespace Jinaga.Test.Model
{
    [FactType("Skylane.Refund")]
    public record Refund(Booking booking, DateTime dateRefunded)
    {
    }
}