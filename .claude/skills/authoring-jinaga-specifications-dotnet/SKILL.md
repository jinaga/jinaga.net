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
(live).

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

## Projections combine multiple relations in one round trip

```csharp
Given<Project>.Match(project =>
    project.Successors().OfType<Task>(t => t.project)
        .Select(task => new
        {
            TaskId = j.Hash(task),
            Title = task.Assignments... // relations defined per authoring-jinaga-facts-dotnet
        }))
```

Prefer one specification that projects everything a view needs over several
separate queries — each `j.Query()`/`j.Watch()` call is its own round trip.

## The hard limit: no post-processing inside a specification

Specifications support `.Select()`, `.Where()` (relationship filtering, not
field-value comparisons), and multiple `from` joins — and nothing else.
There is no `.Count()`, `.OrderBy()`, `.First()`, `.GroupBy()`, or `.Join()`
inside a specification, and no comparing a field's *value* with `.Where()`.
Sorting, counting, grouping, and picking the first of a set all happen in C#
after the specification has materialized results into a list:

```csharp
var tasks = await j.Query(spec, project);        // materialize first
var sorted = tasks.OrderBy(t => t.CreatedAt);     // then transform
```

Reaching for a specification-side `.Where(x => x.Value == "done")` or
`.OrderBy(...)` inside `Given<T>.Match(...)` is a sign the query needs to be
split into "fetch" and "shape" steps rather than one step.
