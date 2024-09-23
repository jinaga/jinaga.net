module SpecificationTests

open Jinaga
open Xunit

[<FactType("Skylane.Airline")>]
type Airline =
    {
        identifier: string
    }

[<Fact>]
let CanSpecifyIdentity() =
    let specification = Given<Airline>.Select(
        fun airline facts -> airline
    )
    Assert.Equal(
        """(airline: Skylane.Airline) {
} => airline
""",
        specification.ToString().ReplaceLineEndings()
    )