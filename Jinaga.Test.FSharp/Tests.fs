module Tests

open System
open System.Linq
open Jinaga
open Xunit

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


[<Fact>]
let ``My test`` () =
    let specificationExpression =
        fun (company : Company) (facts : Repository.FactRepository) ->
            facts.OfType<Employee>().Where(fun x -> x.Company = company)
    let employeesOfCompany =
        Given<Company>.Match<Employee>(
            specificationExpression
        )

    let j = JinagaClient.Create()

    let employees = j.Query(employeesOfCompany, { Identifier = "Contoso" }).Result

    for employee in employees do
        printfn $"%A{employee}"
