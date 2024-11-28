# Jinaga.NET

Resilient, reliable data transfer for .NET.

## What it Does

In [Jinaga](https://jinaga.com), you define a data model in terms of immutable facts.
A fact represents an entity, a change to an entity, or a decision that a user or service has made.

In Jinaga.NET, facts are C# `record`s.:

```C#
[FactType("Corporate.Company")]
record Company(string identifier) {}

[FactType("Corporate.Employee")]
record Employee(Company company, int employeeNumber) {}
```

When a user or service makes a decision, you will add a fact to the system.
This will store it in the local database.
It will also update the local UI.
And it will publish it so that others can learn about the decision.

```C#
var contoso = await j.Fact(new Company("Contoso"));
var jane = await j.Fact(new Employee(contoso, 1));
var bob = await j.Fact(new Employee(contoso, 2));
```

To query facts, write a specification.
Start at a know fact and find all related facts that match that specification.

```C#
var employeesOfCompany = Given<Company>.Match((company, facts) =>
    from employee in facts.OfType<Employee>()
    where employee.company == company
    select employee
);

var contosoEmployees = await j.Query(contoso, employeesOfCompany);
```

A query returns results at a point in time.
If you want to keep a user interface up-to-date, you will need to set up an event-based watch.

```C#
var observer = j.Watch(contoso, employeesOfCompany, employee =>
{
    var component = AddEmployeeComponent(employee);
    return () =>
    {
        RemoveEmployeeComponent(component);
    };
});
```

Finally, if you want to be notified in real time of new information, just change `Watch` to `Subscribe`.

```C#
var subscription = j.Subscribe(contoso, employeesOfCompany, employee =>
{
    var component = AddEmployeeComponent(employee);
    return () =>
    {
        RemoveEmployeeComponent(component);
    };
});
```

The client will open a persistent connection with the server.
The server will notify the client the moment a new employee is hired.
Because the client already set up a watch, the new employee will appear on the UI.

## Running a Replicator

A Jinaga front end connects to a device called a Replicator.
The Jinaga Replicator is a single machine in a network.
It stores and shares facts.
To get started, create a Replicator of your very own using [Docker](https://www.docker.com/products/docker-desktop/).

```
docker pull jinaga/jinaga-replicator
docker create --name my-replicator -p8080:8080 jinaga/jinaga-replicator
docker start my-replicator
```

This creates and starts a new container called `my-replicator`.
The container is listening at port 8080 for commands.
Configure Jinaga to use the replicator:

```C#
var j = JinagaClient.Create(options =>
{
    options.HttpEndpoint = new Uri("http://localhost:8080/jinaga");
});
```

## Roadmap

[Jinaga.NET](https://github.com/jinaga/jinaga.net) is co-evolving with [Jinaga.JS](https://github.com/jinaga/jinaga.js).
Each of these projects has a front end and a back end.
Either front end is intended to work with either back end.
And the back ends are intended to interconnect to form a decision substrate.

![Jinaga Roadmap](./Documentation/JinagaRoadmap.svg)

### Storage

Jiaga.NET currently has support for SQLite and memory storage.
The next storage solution to implement is PostgreSQL to support Docker deployment.
After that, the MS SQL Server implementation will support enterprise solutions that need to keep the journal transactionally consistent with the projection.

## Release

This repository uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) to manage version numbers.
To release a new version of the Jinaga.NET library, create a new release in the format `yyyymmdd.i`.
For example, `20240917.1`.
All of the packages will be versioned separately.
Their version numbers will not be updated if they have not changed.
The packages will be published to [NuGet](https://www.nuget.org/packages/Jinaga/).

To accomplish this from the command line, use the following `gh` command.

```powershell
gh release create 20240917.1 --generate-notes
```

## Using the Custom Azure Functions Binding

The `JinagaFunctionBinding` library provides a custom Azure Functions binding for Jinaga. Follow these steps to use it in your Azure Functions application:

1. Install the `JinagaFunctionBinding` NuGet package in your Azure Functions application.

```powershell
dotnet add package JinagaFunctionBinding
```

2. Register the subscription configurations in `Startup.cs`.

```csharp
using JinagaFunctionBinding;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Startup))]

namespace YourNamespace
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton<JinagaBindingConfig>(config =>
            {
                var bindingConfig = new JinagaBindingConfig();
                bindingConfig.RegisterSpecification("YourSpecification", "YourSpecificationValue");
                bindingConfig.RegisterStartingPoint("YourStartingPoint", "YourStartingPointValue");
                return bindingConfig;
            });
        }
    }
}
```

3. Write a function using the custom binding.

```csharp
using JinagaFunctionBinding;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace YourNamespace
{
    public static class YourFunction
    {
        [FunctionName("YourFunctionName")]
        public static void Run(
            [JinagaTrigger("YourSpecification", "YourStartingPoint")] JinagaListener listener,
            ILogger log)
        {
            log.LogInformation("Function triggered by Jinaga.");
            // Your function logic here
        }
    }
}
```

4. Deploy the Azure Function and test it by publishing relevant facts to the Jinaga service.
