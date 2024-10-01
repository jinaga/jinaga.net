namespace Jinaga.Test.CSharp;

using FluentAssertions;
using Jinaga.FSharp;
using Jinaga.Test.CSharp.Model;

public class SpecificationTest
{
    [Fact]
    public void CanSpecifyIdentity()
    {
        Specification<Airline, Airline> specification = GivenFS<Airline>.Select((airline, facts) => airline);
        specification.ToString().ReplaceLineEndings().Should().Be(
            """
            (airline: Skylane.Airline) {
            } => airline

            """
            );
    }
}