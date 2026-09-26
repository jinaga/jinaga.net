---
name: authoring-jinaga-facts-dotnet
description: >
  Use when defining or editing a Jinaga.NET fact type in C# — creating a new
  fact class, adding a predecessor or field to an existing one, or modeling
  something that changes over time.
---

# Authoring Jinaga facts in .NET

Translating a fact model (see `designing-historical-models`) into Jinaga.NET
C#. If you haven't modeled the fact types yet, do that first — this skill
covers the syntax and idioms once you know what you're declaring.

## Declare facts as records

Every fact is a `record`, never a `class`. Properties are immutable and set
only through the constructor — a fact must never gain a method that mutates
it, because nothing about a fact is allowed to change after it's created.

```csharp
[FactType("TaskTracker.Project")]
public record Project(User creator, DateTime createdAt);

[FactType("TaskTracker.Task")]
public record Task(Project project, DateTime createdAt);
```

`[FactType]` names use a dotted, hierarchical string mirroring the entity's
conceptual nesting (`TaskTracker.Task.Assignment`, not `TaskAssignment`) —
this keeps related fact types sorting and grouping together wherever the
type name is displayed.

## Predecessors are constructor parameters

A predecessor is just another fact type used as a constructor parameter —
no separate attribute is needed. Order predecessors first, by convention:

```csharp
[FactType("TaskTracker.Task.Assignment")]
public record TaskAssignment(Task task, User assignee, DateTime assignedAt);
```

## Disambiguate facts that would otherwise collide

Jinaga identifies a fact by the hash of its fields and predecessors — two
facts with identical content are the *same* fact, not two instances. For an
"anchor" fact that could otherwise be created twice with identical content
(two projects created by the same user with no other distinguishing field),
include a `DateTime createdAt` purely to keep them distinct:

```csharp
// Without createdAt, two projects created by the same user with no
// other field would collapse into a single fact.
[FactType("TaskTracker.Project")]
public record Project(User creator, DateTime createdAt);
```

This isn't a timestamp for display purposes (though it can serve as one) —
it's there specifically so identity doesn't collapse.

## Mutable properties: the `prior` chain

A value that changes over time — a task's title, its due date — is its own
fact type with a `prior` array of the same type, pointing at whatever it
supersedes:

```csharp
[FactType("TaskTracker.Task.Title")]
public record TaskTitle(Task task, string value, TaskTitle[] prior);
```

The first title for a task is created with `prior: []`. Renaming creates a
new `TaskTitle` with `prior: [previousTitle]`. Two concurrent edits both
point at the same prior title — creating a fork — and a later merge fact
lists *both* forked titles in `prior`, never just the one that "won". See
`authoring-jinaga-specifications-dotnet` for the `WhereCurrent` query that
reads only the un-superseded value(s), and `reviewing-model-idioms` for what
goes wrong when that filter is skipped.

Never pass just the winning value into `prior` when merging a fork — that
silently drops the information that a concurrent edit happened, and a
different client that only saw the dropped value has no way to detect the
merge.

## Delete and restore

```csharp
[FactType("TaskTracker.Task.Deletion")]
public record TaskDeletion(Task task, DateTime deletedAt);

[FactType("TaskTracker.Task.Restoration")]
public record TaskRestoration(TaskDeletion taskDeletion, DateTime restoredAt);
```

A task is live if it has no `TaskDeletion`, or has one that itself has a
`TaskRestoration` — a nested existential, covered in
`authoring-jinaga-specifications-dotnet`.

## Encapsulate relations on the fact type

Rather than repeating a query fragment everywhere it's needed, define it once
as a `Relation<T>` on the fact type it starts from:

```csharp
public record Task(Project project, DateTime createdAt)
{
    public Relation<TaskAssignment> Assignments => Relation.Define(_ =>
        this.Successors().OfType<TaskAssignment>(a => a.task));
}
```

For a framework type you don't own (`Jinaga.User`), attach the relation via a
C# extension member instead of editing the type:

```csharp
public static class UserExtensions
{
    extension(User user)
    {
        public Relation<Project> Projects => Relation.Define(_ =>
            user.Successors().OfType<Project>(p => p.creator));
    }
}
```

## Schema evolution

Adding a new *optional* field to an existing fact type doesn't change the
hash of facts that don't set it — Jinaga skips `null` fields when hashing.
A new required field, or a new predecessor, does change the hash: it defines
a new, distinct fact type in every way that matters, even if the
`[FactType]` string is unchanged. When adding a field, decide explicitly
whether existing facts should still match — if they should, the field must
be optional.
