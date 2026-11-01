---
name: authoring-factual-specifications
description: >
  Use when designing a Jinaga specification, authorization rule, or
  distribution rule before writing it in C# or TypeScript — deciding what
  a query matches, what it projects, which facts must or must not exist, or
  who may create or read a fact. Language-agnostic; the per-language
  specification and rules skills translate the result.
---

# Authoring specifications in Factual

A specification is a declarative query over the fact graph. Factual is the
notation Jinaga uses to describe one, in any language: `ToString()` on a C#
specification prints it, the JavaScript `describe` functions print it, and a
replicator's policy files are written in it. Write the specification in
Factual first, check it against the rules below, then translate it with
`authoring-jinaga-specifications-dotnet` or
`authoring-jinaga-specifications-typescript`. The rules here are properties
of the language, so they hold in both.

## Shape

```factual
(project: TaskTracker.Project) {
    task: TaskTracker.Task [
        task->project: TaskTracker.Project = project
        !E {
            deletion: TaskTracker.Task.Deletion [
                deletion->task: TaskTracker.Task = task
            ]
        }
    ]
} => {
    id = #task
    createdAt = task.createdAt
    titles = {
        title: TaskTracker.Task.Title [
            title->task: TaskTracker.Task = task
            !E {
                next: TaskTracker.Task.Title [
                    next->prior: TaskTracker.Task.Title = title
                ]
            }
        ]
    } => title.value
}
```

- **Givens** in parentheses are the starting point.
- **Unknowns** (`task: TaskTracker.Task`) are declared one at a time, each
  with its conditions in square brackets.
- A **path condition** joins the unknown to a label already in scope by
  walking predecessor roles: `->role: Type` steps up one edge. Use as many
  steps as needed on either side.
- An **existential condition** (`E { … }` or `!E { … }`) requires that some
  tuple does, or does not, exist.
- The **projection** after `=>` is a label, a field (`task.createdAt`), a
  hash (`#task`), a nested specification, or a composite `{ name = … }` of
  these.

## Labels are explicit

A fact can be used only once it has been declared as a label: a given, or
an unknown in a match. This is by design. The declaration is what makes the
fact part of the specification's results, so introducing one implicitly
would change what the specification means.

- **Project only declared labels.** `title.value` and `#task` are valid
  because `title` and `task` are declared. `task->project.createdAt` is
  not: declare `project: TaskTracker.Project [ project = task->project:
  TaskTracker.Project ]`, then project `project.createdAt`.
- **Follow a relation from a declared label.** To read the members of a
  task's project, declare the project as an unknown first, then match its
  members against it.
- **Attach a condition to the unknown it constrains.** An existential
  condition belongs to a declared unknown. A condition that seems to be
  about a given is written on an unknown that path conditions tie to that
  given.
- **Join only to labels in scope.** A path condition names a given, an
  earlier unknown, or, inside an existential condition, the unknown it
  belongs to.

## What a specification does not do

These are also by design. Each would make a result depend on something
other than which facts exist.

- **No comparing field values.** Conditions relate facts by their
  predecessors and by existence. "Due before Friday" and "status equals
  done" are not conditions. Model the decision as a fact
  (`TaskCompletion`) and test that it exists, or filter on the field
  after the results are read.
- **No sorting, counting, grouping, or picking the first.** Shape the
  results in code after reading them.

## Patterns

**Current value of a mutable property.** The value no later value lists in
its `prior`, as `titles` above shows. During a concurrent edit there can be
more than one; the code that reads the results picks which to display.

**Live entity (delete and restore).** Not deleted, or deleted and restored:

```factual
!E {
    deletion: TaskTracker.Task.Deletion [
        deletion->task: TaskTracker.Task = task
        !E {
            restoration: TaskTracker.Task.Restoration [
                restoration->deletion: TaskTracker.Task.Deletion = deletion
            ]
        }
    ]
}
```

**Every X has a Y.** There is no universal condition, and none is needed:
"no X lacks a Y" says the same thing with two negative existentials. This
asks for members who hold every skill the task requires:

```factual
(task: TaskTracker.Task) {
    project: TaskTracker.Project [
        project = task->project: TaskTracker.Project
    ]
    member: TaskTracker.Project.Member [
        member->invitation: TaskTracker.Project.Invitation->project: TaskTracker.Project = project
        !E {
            requirement: TaskTracker.Task.SkillRequirement [
                requirement->task: TaskTracker.Task = task
                !E {
                    certification: TaskTracker.Project.Member.Certification [
                        certification->member: TaskTracker.Project.Member = member
                        certification->skill: TaskTracker.Skill = requirement->skill: TaskTracker.Skill
                    ]
                }
            ]
        }
    ]
} => member
```

## Rules are specifications

**An authorization rule** is a specification whose given is the fact being
created and whose projection is the users who may create it:

```factual
(task: TaskTracker.Task) {
    creator: Jinaga.User [
        creator = task->project: TaskTracker.Project->creator: Jinaga.User
    ]
} => creator
```

A fact type with no rule cannot be created by anyone, including
`Jinaga.User`. Several rules for one type allow anyone any of them names.

A rule runs while its fact is being authorized, before that fact is saved.
So **walk the fact's predecessors before any successor join**. A rule that
seeks successors of the new fact, or reads it inside a negative existential
condition, would find nothing and refuse every write. Both libraries and
the replicator refuse such a rule when it is built or loaded.

**A fact that links two owned facts** needs both to have the same owner.
Put two path conditions on one label:

```factual
(assignment: TaskTracker.Task.Assignment) {
    project: TaskTracker.Project [
        project = assignment->task: TaskTracker.Task->project: TaskTracker.Project
        project = assignment->member: TaskTracker.Project.Member->invitation: TaskTracker.Project.Invitation->project: TaskTracker.Project
    ]
    creator: Jinaga.User [
        creator = project->creator: Jinaga.User
    ]
} => creator
```

Without the second condition, the project's creator could assign a member
of a *different* project, and distribution would then send that member this
project's tasks.

**A distribution rule** pairs the specification being shared with a user
specification from the same given:

```factual
share (project: TaskTracker.Project) {
    task: TaskTracker.Task [
        task->project: TaskTracker.Project = project
    ]
} => task
with (project: TaskTracker.Project) {
    creator: Jinaga.User [
        creator = project->creator: Jinaga.User
    ]
} => creator
```

Share the specifications the application actually runs. A client's query is
permitted when a shared specification covers it.
