---
name: testing-jinaga-dotnet
description: >
  Use when writing or editing a test for a Jinaga.NET fact type, specification,
  authorization or distribution rule, or view model — setting up test data,
  testing concurrent edits, proving who may create or read a fact, or waiting
  for a `j.Watch()` subscription to settle.
---

# Testing Jinaga.NET

Test against `Jinaga.UnitTest`'s in-memory client, not a mock of `JinagaClient`
— the real save/query/watch behavior (including hashing and existential
filtering) is exactly what a test needs to exercise, and a hand-rolled mock
tends to quietly assert the test author's assumptions rather than the
library's real behavior.

## Set up test data with a `GivenXxx` builder

Build a small, reusable async helper per scenario shape, with optional
parameters for the fields a specific test needs to vary:

```csharp
public static async Task<Task> GivenTask(JinagaClient j, string? title = "Untitled",
    bool assigned = false)
{
    var user = await j.Fact(new User("test-user"));
    var project = await j.Fact(new Project(user, DateTime.UtcNow));
    var task = await j.Fact(new global::TaskTracker.Task(project, DateTime.UtcNow));
    if (title != null)
        await j.Fact(new TaskTitle(task, title, prior: []));
    if (assigned)
        await j.Fact(new TaskAssignment(task, user, DateTime.UtcNow));
    return task;
}
```

Keeping this separate from the test body means a test reads as "given a task
[in this state], when [action], then [assertion]" rather than reconstructing
the whole fact graph inline every time.

## A basic test

```csharp
var j = JinagaTest.Create();
var task = await GivenTask(j, title: "Draft outline");

var titles = await j.Query(CurrentTitleQuery(), task);

Assert.Single(titles);
Assert.Equal("Draft outline", titles[0].Title);
```

## Test concurrent edits and their resolution

Because a mutable property forks when two facts point at the same `prior`,
a test suite for anything using the pattern should exercise the fork
explicitly, not just the happy path of one edit at a time:

```csharp
var task = await GivenTask(j, title: null);
var titleA = await j.Fact(new TaskTitle(task, "Draft outline", prior: []));
var titleB = await j.Fact(new TaskTitle(task, "Outline v2", prior: [titleA]));

// A second, concurrent edit against the same prior forks the value.
var titleC = await j.Fact(new TaskTitle(task, "Outline (alt)", prior: [titleA]));

var current = await j.Query(CurrentTitleQuery(), task);
Assert.Equal(2, current.Count); // both titleB and titleC are current until merged

var merged = await j.Fact(new TaskTitle(task, "Outline (final)", prior: [titleB, titleC]));
current = await j.Query(CurrentTitleQuery(), task);
Assert.Single(current);
```

Start the task with no title. A builder that already gave it one would add a
second root alongside `titleA`, and three titles would be current, not two.

## Test the rules with the rules on

`JinagaTest.Create` takes the same rule builders the application uses. With
them set, the test client decides what a replicator would decide:

```csharp
private static JinagaClient As(User user, params object[] alreadyHappened) =>
    JinagaTest.Create(options =>
    {
        options.User = user;
        options.Authorization = JinagaConfig.Authorization;
        options.Distribution = JinagaConfig.Distribution;
        options.InitialState = alreadyHappened.ToImmutableList();
    });
```

- `User` is who is acting.
- `InitialState` holds facts that already happened, as though synced from
  other users. They skip authorization: they are the premises of the test,
  not its actions. "A removed member can't comment" seeds the removal
  instead of first proving who was allowed to remove them.

Assert both sides of every rule, the decision the rule allows and one it
refuses:

```csharp
[Fact]
public async Task ARemovedMemberCannotComment()
{
    var removal = new ProjectMemberRemoval(member, DateTime.UtcNow);
    var j = As(memberUser, project, invitation, member, task, removal);

    await Assert.ThrowsAsync<AuthorizationException>(() =>
        j.Fact(new TaskComment(task, "Still here?", DateTime.UtcNow)));
}

[Fact]
public async Task AStrangerReadsNoTasks()
{
    var j = As(stranger, project, task);

    var (tasks, diagnostics) = await j.QueryWithDiagnostics(ProjectSpecifications.Tasks, project);

    Assert.Empty(tasks);
    Assert.Contains(diagnostics, d => d.Code == DistributionDenialCode.PrincipalExcluded);
}
```

A specification that no rule shares, or that is narrower than the rule
sharing it, makes `j.Query` throw `DistributionDeniedException`. Test each
screen's specification under the user who will run it, so a missing share
fails here rather than at the replicator.

### Replay the example story

Keep the example the model was designed from as an ordered list of
decisions: who acted, and the fact they created. Replay each step as its
actor, with every earlier step as a premise:

```csharp
public record StoryStep(User Actor, object Fact);

[Fact]
public async Task EveryDecisionInTheStoryIsAllowed()
{
    var known = new List<object>();
    foreach (var step in Story.Steps)
    {
        var j = JinagaTest.Create(options =>
        {
            options.User = step.Actor;
            options.Authorization = JinagaConfig.Authorization;
            options.InitialState = known.ToImmutableList();
        });
        await j.Fact(step.Fact);
        known.Add(step.Fact);
    }
}
```

This proves every legitimate decision is allowed; the refusal tests prove
the boundaries. The same list seeds specification tests and demo data.

## Wait for a watch to settle, don't sleep for it

A view model built on `j.Watch()` populates asynchronously. Rather than
`await Task.Delay(...)` and hoping it was long enough, expose a way to await
the initial load explicitly (an `IObserver`-backed `Loaded()` method on the
view model, or an equivalent completion signal), and await that in the test:

```csharp
using var viewModel = await CreateAndLoadViewModel(j, task); // awaits Loaded()
Assert.Equal("Draft outline", viewModel.Title);
```

A test that sleeps for a fixed duration instead is both slower than
necessary when the watch settles quickly, and flaky on a slower machine when
it doesn't.
