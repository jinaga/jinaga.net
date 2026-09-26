---
name: testing-jinaga-dotnet
description: >
  Use when writing or editing a test for a Jinaga.NET fact type, specification,
  or view model — setting up test data, testing concurrent edits, or waiting
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
public static async Task<Task> GivenTask(JinagaClient j, string title = "Untitled",
    bool assigned = false)
{
    var user = await j.Fact(new User("test-user"));
    var project = await j.Fact(new Project(user, DateTime.UtcNow));
    var task = await j.Fact(new global::TaskTracker.Task(project, DateTime.UtcNow));
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
var task = await GivenTask(j);
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
