module SpecificationTests

open System
open System.Collections.Immutable
open Microsoft.FSharp.Quotations
open FSharp.Linq.RuntimeHelpers
open Jinaga
open Jinaga.Projections
open Jinaga.Repository
open Xunit

[<FactType("Skylane.Airline")>]
type Airline =
    {
        identifier: string
    }


type Given<'TFact when 'TFact : not struct>() =
    static member Select<'TProjection>(specSelector: Expr<Func<'TFact, FactRepository, 'TProjection>>) : Specification<'TFact, 'TProjection> =
        let lambdaExpression = LeafExpressionConverter.QuotationToLambdaExpression(specSelector)
        let struct (givens, matches, projection) = SpecificationProcessor.Select<'TProjection>(lambdaExpression)
        let specificationGivens = 
            givens
            |> Seq.map (fun g -> SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
            |> ImmutableList.ToImmutableList
        Specification<'TFact, 'TProjection>(specificationGivens, matches, projection)


[<Fact>]
let CanSpecifyIdentity() =
    let specification = Given<Airline>.Select(
        <@ Func<Airline, FactRepository, Airline>(fun airline facts -> airline) @>
    )
    Assert.Equal(
        """(airline: Skylane.Airline) {
} => airline
""",
        specification.ToString().ReplaceLineEndings()
    )