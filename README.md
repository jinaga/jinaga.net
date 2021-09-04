# Jinaga.NET

Resilient, reliable data transfer for .NET.

## What it Does

In [Jinaga](https://jinaga.com), you define a data model in terms of immutable facts.
A fact represents an entity, a change to an entity, or a decision that a user or service has made.

In Jinaga.NET, facts are C# `record`s.:

```C#
[FactType("Corporate.Company")]
record Company(string identifier);

[FactType("Corporate.Employee")]
record Employee(Company company, int employeeNumber);
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
If you want to keep a user interface up-to-date, you will need to continually watch.

```C#
var observer = j.Watch(contoso, employeesOfCompany, o => o
    .OnAdded(employee => AddEmployeeComponent(employee))
    .OnRemoved(component => RemoveEmployeeComponent(component))
);
```

Finally, if you want to be notified in real time of new information, just subscribe.

```C#
var subscription = j.Subscribe(contoso, employeesOfCompany);
```

The client will open a persistent connection with the server.
The server will notify the client the moment a new employee is hired.
Because the client already set up a watch, the new employee will appear on the UI.

## Roadmap

[Jinaga.NET](https://github.com/jinaga/jinaga.net) is co-evolving with [Jinaga.JS](https://github.com/jinaga/jinaga.js).
Each of these projects has a front end and a back end.
Either front end is intended to work with either back end.
And the back ends are intended to interconnect to form a decision substrate.

![Jinaga Roadmap](./Documentation/JinagaRoadmap.svg)

### APIs

The primary APIs for Jinaga are:

- `j.Fact` - Add and publish a fact
- `j.Query` - Project the facts matching a specification
- `j.Watch` - Continually update a projection
- `j.Subscribe` - Receive continuous updates from peers

The `Subscribe` API is not fully implemented in Jinaga.JS, and not implemented yet in Jinaga.NET.
The JS version uses polling rather than the intended mechanism of Web Sockets or HTTP/2 Server Push.

### Storage

Jinaga.NET currently has only memory storage, which is packaged with the unit testing library.
The next storage solutions to implement are SQLite to support Xamarin mobile apps and PostgreSQL to support Docker deployment.
After that, the MS SQL Server implementation will support enterprise solutions that need to keep the journal transactionally consistent with the projection.

The `IStore` interface for Jinaga.NET currently includes both `Query` and `QueryAll`.
The `QueryAll` method is more general.
All calls to `Query` will be moved over, and then the more specific method will be removed.
Jinaga.JS does not yet have the equivalent of `QueryAll`, so that will come later.

### Pipelines

The pipeline compiler continues to evolve.
I'm using .NET Interactive Notebooks to explore the types of queries that applications will need, and then test drive their implementations.
The next pipeline feature to implement is nested specifications.

The pipeline inverter is currently driven by scenarios as well.
A future project will be to walk through the proof of query inversion presented in [The Art of Immutable Architecture](https://immutablearchitecture.com) and verify that this pipeline inverter is correct.
We will also back port the Jinaga.NET pipeline inverter to Jinaga.JS.

### Rules

Authorization rules -- which limit the users who can create facts -- are implemented in Jinaga.JS, but not yet in Jinaga.NET.
Distribution rules -- which limit the specifications that a user can query -- are not yet implemented in either.