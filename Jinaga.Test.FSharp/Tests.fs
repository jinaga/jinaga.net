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
    let employeesOfCompany =
        Given<Company>.Match(
            (fun (company : Company) (facts : Repository.FactRepository) ->
            facts.OfType<Employee>().Where(fun x -> x.Company = company))
        )

    let j = JinagaClient.Create()

    let employees = j.Query(employeesOfCompany, { Identifier = "Contoso" }).Result

    for employee in employees do
        printfn $"%A{employee}"
