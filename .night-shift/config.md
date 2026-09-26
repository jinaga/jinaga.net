# Night shift configuration

The `night-shift-worker`, `night-shift-coordinator`, and `refining-issues`
skills read this file. Every `##` heading below except `## Branch prefix` is
required. A missing heading is an error, not a default: stop and name the
heading you could not find. Anything that is not one of these headings is
protocol, and protocol lives in the skill.

## Visibility

`public`

There is nothing to withhold here. Some sibling repositories in this practice
may be private, so never carry their contents into an issue or pull request in
this one.

## Issue label

`ready`

## Read first

- `Building.md` — build, test, and versioning commands for the monorepo.
- `.claude/rules/learned.md` — conventions and landmines learned on this
  project.
- The skills under `.claude/skills/` that match the change:
  `authoring-jinaga-facts-dotnet`, `authoring-jinaga-specifications-dotnet`,
  and `testing-jinaga-dotnet`.
- The documents under `## Authority`, at the articles the issue cites.

## Authority

- `.claude/skills/degrees-of-freedom/degrees-of-freedom-constitution.md` — the
  principles a design answers to. An issue's **Conformance** section cites the
  article each criterion answers to. An amendment to it is the maintainer's
  decision and never lands in an implementation slice.

## Before you fix

Reproduce first, with a failing test in `Jinaga.Test` (or
`Jinaga.Store.SQLite.Test` for the SQLite store). An issue's repro may have been
reconstructed rather than verified by its reporter, so a repro that fails is a
finding, not a failure. Report it and open no pull request.

This is a published library, so a defect a reporter hit on a released version
may already be fixed on `main`. Check which version the report ran against
before concluding the shape is broken here.

## Verification commands

Green before every push, in this order. They are the steps `build-test.yml`
runs.

```
dotnet restore
dotnet build Jinaga --no-restore
dotnet build Jinaga.UnitTest --no-restore
dotnet build Jinaga.Test --no-restore
dotnet build Jinaga.Notebooks --no-restore
dotnet build Jinaga.Tool --no-restore
dotnet build Jinaga.Store.SQLite --no-restore
dotnet build Jinaga.Store.SQLite.Test --no-restore
dotnet build Jinaga.Maui --no-restore
dotnet test Jinaga.Test --no-build --verbosity normal
dotnet test Jinaga.Store.SQLite.Test --no-build --verbosity normal
```

The solution needs both the .NET 8 and .NET 10 SDKs. `NuGet.config` names a
`GitHub` package source at `https://nuget.pkg.github.com/Jinaga/index.json`;
CI authenticates to it with the workflow token before restoring. If
`dotnet restore` fails on that source with an authorization error, that is an
environment finding to report, not a reason to edit `NuGet.config`.

## CI workflow

`build-test.yml`

Read runs for this file; it is the merge gate. It runs on `push` to `main` and
on `pull_request` with no `branches:` filter, so every layer of a stack gets
check runs from its own pull request event. It also has `workflow_dispatch` as
the escape hatch for a stacked pull request whose checks never fired.

Three workflows live in `.github/workflows/`, and the other two (`nuget.yml`,
`release.yml`) are release and publish jobs that do not gate a pull request.
Address the workflow by this file name rather than by a display name.

## After the pull request

`none`

## What is different about this repository

This is a maintained library whose queue mixes defect reports from people
outside the project with work the maintainer has already sequenced. An issue
may not reproduce at all, and saying so with evidence is the whole result. Do
not manufacture a fix to justify the session.

Attribution is easy to get wrong. When a fix appears to resolve an issue, check
that the issue's shape was actually broken at the version the reporter ran,
rather than assuming the nearest merged pull request is the cause.

Each project in this monorepo has its own `version.json`, and its version is
computed from the git height of its own path. Do not edit a `version.json` to
change a version number; releases are cut by the maintainer.
