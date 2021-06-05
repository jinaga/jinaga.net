using System;
using System.Linq;
using Jinaga.Test.Model;
using Xunit;

namespace Jinaga.Test
{
    public class SpecificationTest
    {
        [Fact]
        public void CanSpecifySuccessors()
        {
            Specification<Airline, Flight> specification = Specification.From<Airline>().To((airline, facts) =>
                from flight in facts.OfType<Flight>()
                where flight.AirlineDay.Airline == airline
                select flight
            );
        }
    }
}
