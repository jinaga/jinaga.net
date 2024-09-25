module Tests

open System.Linq.Expressions
open Microsoft.FSharp.Quotations
open Jinaga
open Xunit
open Microsoft.FSharp.Linq.RuntimeHelpers
open Jinaga.Projections
open System.Collections.Immutable
open System
open Jinaga.Repository
open System.Linq

[<FactType("Corporate.Company")>]
type Company =
    {
        Identifier : string
    }

[<FactType("Corporate.Employee")>]
type Employee =
    {
        Company : Company
        EmployeeNumber : int
    }

let rec translateExpression (expr: Expr) : Expression =
    match expr with
    | Patterns.Call(instance, methodInfo, args) -> 
        let translatedInstance = 
            instance |> Option.map translateExpression |> Option.toObj
        let translatedArgs = args |> List.map translateExpression |> List.toArray
        Expression.Call(translatedInstance, methodInfo, translatedArgs)
    
    | Patterns.Lambda(param, body) -> 
        let translatedParam = translateParameter param
        let translatedBody = translateExpression body
        Expression.Lambda(translatedBody, translatedParam)

    | Patterns.Let(var, valueExpr, bodyExpr) -> 
        // Inline the "let" into an expression tree as there is no direct mapping in C#
        let translatedVar = translateParameter var
        let translatedValue = translateExpression valueExpr
        let translatedBody = translateExpression bodyExpr
        Expression.Invoke(
            Expression.Lambda(translatedBody, translatedVar), 
            translatedValue)

    | Patterns.PropertyGet(instance, propertyInfo, _) -> 
        let translatedInstance = instance |> Option.map translateExpression |> Option.toObj
        Expression.Property(translatedInstance, propertyInfo)
    
    | Patterns.Value(value, _) ->
        Expression.Constant(value)

    | Patterns.Application(funcExpr, argExpr) ->
        let translatedFunc = translateExpression funcExpr
        let translatedArg = translateExpression argExpr
        Expression.Invoke(translatedFunc, translatedArg)

    | Patterns.LetRecursive _ -> failwith "LetRecursive is not supported in C# expression trees"
    
    | _ -> failwithf "Unsupported expression type: %A" expr

and translateLambda (expr: Expr) : LambdaExpression =
    match expr with
    | Patterns.Lambda(param, body) -> 
        let translatedParam = translateParameter param
        let translatedBody = translateExpression body
        Expression.Lambda(translatedBody, translatedParam)
    | _ -> failwith "Expected a lambda expression"

and translateParameter (param: Var) : ParameterExpression =
    Expression.Parameter(param.Type, param.Name)

type Given<'TFact when 'TFact : not struct>() =
    static member Match<'TProjection>(specExpression: Expr<'TFact -> FactRepository -> IQueryable<'TProjection>>) : Specification<'TFact, 'TProjection> =
        let lambdaExpression = LeafExpressionConverter.QuotationToLambdaExpression(specExpression)
        let struct (givens, matches, projection) = SpecificationProcessor.Queryable<'TProjection>(lambdaExpression)
        let specificationGivens = 
            givens
            |> Seq.map (fun g -> SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
            |> ImmutableList.ToImmutableList
        Specification<'TFact, 'TProjection>(specificationGivens, matches, projection)

    static member Match<'TProjection>(specExpression: Expression<Func<'TFact, FactRepository, IQueryable<'TProjection>>>) : Specification<'TFact, 'TProjection> =
        let struct (givens, matches, projection) = SpecificationProcessor.Queryable<'TProjection>(specExpression)
        let specificationGivens = 
            givens
            |> Seq.map (fun g -> SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
            |> ImmutableList.ToImmutableList
        Specification<'TFact, 'TProjection>(specificationGivens, matches, projection)

    static member Select<'TProjection>(specSelector: Expr<'TFact -> FactRepository -> 'TProjection>) : Specification<'TFact, 'TProjection> =
        let lambdaExpression = LeafExpressionConverter.QuotationToLambdaExpression(specSelector)
        let struct (givens, matches, projection) = SpecificationProcessor.Select<'TProjection>(lambdaExpression)
        let specificationGivens = 
            givens
            |> Seq.map (fun g -> SpecificationGiven(g, ImmutableList<ExistentialCondition>.Empty))
            |> ImmutableList.ToImmutableList
        Specification<'TFact, 'TProjection>(specificationGivens, matches, projection)


[<Fact>]
let ``My test`` () =
    let employeesOfCompany =
        Given<Company>.Match<Employee>(
            fun company facts ->
                query {
                    for employee in facts.OfType<Employee>() do
                    where (employee.Company = company)
                    select employee
                }
        )

    let j = JinagaClient.Create()

    let employees = j.Query(employeesOfCompany, { Identifier = "Contoso" }).Result

    for employee in employees do
        printfn $"%A{employee}"
