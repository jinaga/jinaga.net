namespace Jinaga.FSharp

open System
open System.Linq.Expressions

module Given =
    type TFact = obj

    let matchExpression<'TFact, 'TProjection> (specExpression: Expression<Func<'TFact, FactRepository, IQueryable<'TProjection>>>) =
        // Implementation

    let matchExpressionSimple<'TFact, 'TProjection> (specExpression: Expression<Func<'TFact, 'TProjection>>) =
        // Implementation

    let selectExpression<'TFact, 'TProjection> (specSelector: Expression<Func<'TFact, FactRepository, 'TProjection>>) =
        // Implementation

module Given2 =
    type TFact1 = obj
    type TFact2 = obj

    let matchExpression<'TFact1, 'TFact2, 'TProjection> (specExpression: Expression<Func<'TFact1, 'TFact2, FactRepository, IQueryable<'TProjection>>>) =
        // Implementation

    let matchExpressionSimple<'TFact1, 'TFact2, 'TProjection> (specExpression: Expression<Func<'TFact1, 'TFact2, 'TProjection>>) =
        // Implementation

    let selectExpression<'TFact1, 'TFact2, 'TProjection> (specSelector: Expression<Func<'TFact1, 'TFact2, FactRepository, 'TProjection>>) =
        // Implementation

type Specification<'TFact, 'TProjection>() =
    member this.ToDescriptiveString(given: 'TFact) =
        // Implementation

type Specification2<'TFact1, 'TFact2, 'TProjection>() =
    member this.ToDescriptiveString(given1: 'TFact1, given2: 'TFact2) =
        // Implementation
