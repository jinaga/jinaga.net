---
name: authoring-jinaga-specifications-dotnet
description: >
  Use when writing or editing a Jinaga.NET specification in C# — querying
  facts, projecting fields, filtering to the current value of a mutable
  property, or wiring a specification to `j.Query()` / `j.Watch()`.
---

# Authoring Jinaga specifications in .NET

Specifications are declarative queries over the fact graph, built with
`Given<T>.Match(...)` and run with `j.Query()` (one-shot) or `j.Watch()`
(live). Design the specification in Factual first
(`authoring-factual-specifications`): the rules there, such as explicit
labels and no field comparisons, are what this C# must express. Calling
`ToString()` on a specification prints its Factual, which is the quickest
way to check a translation.

## Basic shape

```csharp
using Jinaga.Extensions; // Successors(), OfType()
using Jinaga.Patterns;   // WhereCurrent(), WhereNotDeleted(), WhereNotDeletedOrRestored()

private static Specification<Task, TaskProjection> CurrentTitleQuery() =>
    Given<Task>.Match(task =>
        from title in task.Successors().OfType<TaskTitle>(t => t.task)
            .WhereCurrent(next => next.prior)
        select new TaskProjection(title.value)
    );
```

`Successors().OfType<T>(lambda-back-to-predecessor)` walks the graph
forward from a given fact to everything that points back at it. The lambda
tells the query which predecessor role to walk — required because a fact
type can have more than one predecessor of the same type.

The same query can start from the `FactRepository` instead, which is the form
`Relation.Define` and `Condition.Define` use and the one to reach for when a
query joins several unknowns:

```csharp
Given<Task>.Match((task, facts) =>
    from title in facts.OfType<TaskTitle>(t => t.task == task)
        .WhereCurrent(next => next.prior)
    select new TaskProjection(title.value))
```

## Declare a label before using it

Each `from x in ...` declares a label. To project a predecessor's fields, or
follow a relation defined on it, declare the predecessor first and join it
by its path:

```csharp
Given<Task>.Match((task, facts) =>
    from project in facts.OfType<Project>()
    where project == task.project
    from member in project.Members
    select member)
```

`task.project.Members` is refused when the specification is built ("Cannot
select project directly. Give the fact a label first."). That is the
Factual rule that a fact must be declared before it is used, not a parser
limitation.

## Name a condition once

A `Condition` defined on a fact type (`authoring-jinaga-facts-dotnet`) reads
as a boolean in `where`:

```csharp
from task in facts.OfType<Task>(t => t.project == project)
where !task.IsCompleted && !task.IsDeleted
select task
```

## Reading the current value of a mutable property

`.WhereCurrent(next => next.prior)` is the existential filter that excludes
any fact something else lists in its `prior` array — this is how "the
current title" is expressed, and it's the filter `reviewing-model-idioms`
calls out as the one most often missing:

```csharp
task.Successors().OfType<TaskTitle>(t => t.task)
    .WhereCurrent(next => next.prior)
```

Apply this at every point a mutable-property fact type is read, not just
once somewhere in the file — a second read site that omits it will return
every historical value, not just the current one.

## Delete/restore: use the built-in pattern helper, don't hand-roll it

`Jinaga.Patterns` ships `WhereNotDeletedOrRestored` specifically for the
delete/restore pattern — reach for it instead of writing the nested
existential by hand:

```csharp
Given<Project>.Match(project =>
    project.Successors().OfType<Task>(t => t.project)
        .WhereNotDeletedOrRestored(
            (TaskDeletion d) => d.task,
            (TaskRestoration r) => r.taskDeletion)
)
```

Both selectors point *from* the deletion/restoration fact *back* to what it
deletes or restores — the same predecessor direction as `OfType`'s own
selector, not a `Successors()` walk forward. Read the semantics as: a `Task`
is excluded if it has a `TaskDeletion`, unless that `TaskDeletion` itself has
a `TaskRestoration`. There's a plain `WhereNotDeleted` too, for a fact type
that's only ever deleted, never restored.

## Project everything a view needs in one specification

Project into a record. A collection parameter holds a nested specification:

```csharp
public record TaskCard(
    Task task,
    IEnumerable<TaskTitle> titles,
    IEnumerable<AssignmentCard> assignments);

public record AssignmentCard(TaskAssignment assignment, IEnumerable<UserName> names);

Given<Project>.Match((project, facts) =>
    from task in facts.OfType<Task>(t => t.project == project)
    select new TaskCard(
        task,
        facts.OfType<TaskTitle>(t => t.task == task).WhereCurrent(next => next.prior),
        from assignment in facts.OfType<TaskAssignment>(a => a.task == task)
        select new AssignmentCard(
            assignment,
            facts.OfType<UserName>(n => n.user == assignment.member.invitation.invitee)
                .WhereCurrent(next => next.prior))))
```

- **`IEnumerable<T>`** holds the collection's contents when the result is
  read. Use it with `j.Query()`.
- **`IObservableCollection<T>`** keeps receiving additions and removals
  under `j.Watch()`. Use it when a live view must show changes *inside* a
  row, such as a new assignment on a task already on screen
  (`integrating-jinaga-reactive-viewmodels-dotnet`). Wrap the nested query
  in `facts.Observable(...)`:
  `facts.Observable(facts.OfType<TaskAssignment>(a => a.task == task))`.
- **`Given<T>.Select((x, facts) => new Record(...))`** projects exactly one
  row for the given, for a view of a single fact.
- A field (`task.createdAt`) or a hash (`j.Hash(task)`) can be projected
  from any declared label.

A current-value collection can hold more than one value while concurrent
edits are unmerged. The code that reads it chooses what to display, and
the fork stays visible to whoever resolves it.

Prefer one specification that projects everything a view needs over several
separate queries. Each `j.Query()` or `j.Watch()` call is its own round trip.

## Sort, count, and compare values after the query

A specification relates facts; it doesn't sort, count, group, pick the
first, or compare a field's value (`authoring-factual-specifications`
explains why). LINQ operators that would do those things compile inside
`Given<T>.Match(...)` but are refused when the specification is built. Read
the results, then shape them in C#:

```csharp
var tasks = await j.Query(spec, project);        // read first
var sorted = tasks.OrderBy(t => t.CreatedAt);     // then shape
```
