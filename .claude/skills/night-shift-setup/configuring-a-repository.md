# Configuring a tracked repository

Every tracked repository carries `.night-shift/config.md` at its root. The worker reads it from its checkout; the coordinator reads the same file over the hosting API; `refining-issues` reads it before it audits anything. There is one copy, in the repository it describes, so the roles cannot disagree about it.

It sits outside `.claude/skills/`, so reinstalling a skill cannot clobber it, and outside `.claude/` entirely, because that directory is agent-harness configuration and this is protocol configuration a person maintains.

## Contents

- [The headings](#the-headings)
- [What is deliberately not here](#what-is-deliberately-not-here)
- [Worked example](#worked-example)

## The headings

Every heading in the table below is required. `## Branch prefix` is recognized too, and is optional.

**A missing required heading is a stop, not a default.** A worker that cannot find one names it and opens no pull request. `none` and `Nothing.` are legal values; absence is not, because absence cannot be told apart from an oversight.

| Heading | Why it cannot be derived or defaulted |
|---|---|
| `## Visibility` | `private` carries a rule against quoting the repository's contents into any public place. A wrong guess is a leak, and nothing inside the container can tell you. |
| `## Issue label` | A wrong guess silently sweeps the wrong queue, or nothing at all. |
| `## Read first` | Which files a worker must read before touching an issue. One repository points at a build guide, another at an accepted specification, another at nothing. |
| `## Authority` | The documents an issue must answer to, and what citing each one means. `refining-issues` runs R4 against this and nothing else. It is not derivable: which document governs, and whether citing it means naming a section or citing a principle, is a decision about how the project is run. `none` is a complete answer, and it makes R4 vacuous rather than failed. |
| `## Before you fix` | Where "reproduce first" is replaced by something else, such as conformance criteria a change is held to. |
| `## Verification commands` | The exact commands, in order, that must be green before every push. |
| `## CI workflow` | The workflow *file* that gates the merge. Deriving it means parsing every workflow in the repository at whatever hour the run wakes. |
| `## After the pull request` | Post-pull-request hooks. This is the heading the rule exists for: `none` says a person decided there is nothing, while absence says nobody looked. |
| `## What is different about this repository` | Free prose for anything the protocol does not fix. `Nothing.` is a complete answer. |

`## Branch prefix` is optional and defaults to `claude/issue-<number>-<slug>`. It is cosmetic, and a wrong guess costs nothing.

## What is deliberately not here

Four values a reader might expect. Each is derivable, and a stored copy can only be wrong later.

- **The repository's owner and name** come from `git remote get-url origin`.
- **Whether `gh` is installed** comes from `command -v gh`.
- **Whether stacked pull requests are enabled** comes from `register-stack.sh list` returning exit 3.
- **Whether a workflow strips the queue label on close** changes nothing a run does, because the protocol always filters for open issues.

**Anything that is not one of those headings belongs in the skill.** This file is where a second, per-repository protocol would grow if it were allowed to, and the closed list is what prevents it.

## Worked example

````markdown
# Night shift configuration

The `night-shift-worker`, `night-shift-coordinator`, and `refining-issues`
skills read this file. Every `##` heading below except `## Branch prefix` is
required. A missing heading is an error, not a default: stop and name the
heading you could not find. Anything that is not one of these headings is
protocol, and protocol lives in the skill.

## Visibility

`private`

Never quote this repository's contents into a public repository, issue, or
pull request. Its sibling repositories are public.

## Issue label

`ready`

## Read first

- `CONTRIBUTING.md` — build commands, subsystem layout, testing rules.

## Authority

- `docs/architecture.md` — the accepted design. An issue names the section it
  answers to wherever one covers the change. An amendment to it is the
  maintainer's decision and never lands in an implementation slice.

## Before you fix

Reproduce first. An issue's repro may have been reconstructed rather than
verified by its reporter, so a repro that fails is a finding, not a failure.

## Verification commands

Both green before every push, in this order.

```
npm run typecheck
npm test
```

`npm test` builds first, so a fresh container needs no separate build step.
`npm run typecheck` reaches scripts the workspace build leaves out.

## CI workflow

`ci.yml`

Read runs for this file; it is the merge gate. A second workflow also runs on
pull requests, and neither carries a `branches:` filter, so every layer of a
stack gets check runs from its own pull request event.

## After the pull request

`none`

## What is different about this repository

Issues carry their own dependencies. Many close with a `Depends on #<n>` line,
and epics carry an order clause the maintainer rewrites as sessions find
things, so read the clause on each sweep rather than trusting a remembered
sequence.
````
