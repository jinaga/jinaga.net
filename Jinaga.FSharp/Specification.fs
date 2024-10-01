namespace Jinaga.FSharp

open System
open System.Linq.Expressions
open System.Collections.Immutable
open Jinaga
open Jinaga.Repository
open Jinaga.Projections

type GivenFS<'TGiven when 'TGiven: not struct>() =
    static member Select<'TResult>(selector: Expression<Func<'TGiven, FactRepository, 'TResult>>) : Specification<'TGiven, 'TResult> =
        let (givens, matches, projection) = SpecificationProcessorFS.SpecificationProcessorFS.Select<'TResult>(selector)
        let specificationGivens = 
            givens
            |> Seq.map (fun g -> SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
            |> ImmutableList.CreateRange
        
        Specification<'TGiven, 'TResult>(specificationGivens, matches, projection)
