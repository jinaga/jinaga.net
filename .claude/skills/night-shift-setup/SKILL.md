---
name: night-shift-setup
description: Stands up the night-shift protocol for an organization, from nothing to a running nightly sweep. Creates the Night Shift Log application from a committed seed, registers the practice and each tracked repository exactly once, writes the per-repository configuration file the worker refuses to run without, and creates the scheduled sessions that fire the sweep. Use when adopting the night-shift protocol for the first time, when adding a repository to an existing practice, or when a run stopped because it found no practice or no repository matching the one it was sweeping.
---

# Night Shift Setup

The night shift is a coordinator that sweeps a labelled issue queue and one worker per repository that takes a single issue to a pull request. Neither keeps state between runs, so both read a log, and the log is an application you create once and share across every repository you track.

This skill covers the one-time acts. `night-shift-coordinator` and `night-shift-worker` cover everything that happens nightly.

## When to use

- **Adopting the protocol** for the first time in an organization.
- **Adding a repository** to a practice that already exists.
- **A run stopped** because it found no practice, no matching repository, or more than one of either.
- **A run stopped** because a repository's configuration file was missing a required heading.

---

## What you are building

| Piece | Where it lives | How many |
|---|---|---|
| The Night Shift Log application | your Factual workspace | one per organization |
| A practice, and one repository record per tracked repository | inside that application | one practice, one record each |
| `.night-shift/config.md` | each tracked repository | one per repository |
| `.night-shift/routines.md` | the coordinator's own repository | one |
| The installed skills | committed into each repository | worker in each tracked repository, coordinator in its own |
| The scheduled sessions | your agent host | one coordinator, one worker per repository |

```
Task Progress:
- [ ] Create the log application and publish the seed
- [ ] Create the practice, exactly once
- [ ] Register each tracked repository, exactly once
- [ ] Write .night-shift/config.md in each tracked repository
- [ ] Install and commit night-shift-worker and refining-issues in each tracked repository
- [ ] Install and commit night-shift-coordinator in its own repository
- [ ] Write .night-shift/routines.md
- [ ] Create the sessions and fire the coordinator once by hand
```

---

## Create the log

Read **[creating-the-log.md](creating-the-log.md)**. It publishes `log-model.factual` into a new application, then runs the practice and repository ceremony.

**Two calls in that ceremony are not idempotent.** Creating a practice and registering a repository both mint a new record every time they run, and a duplicate splits your history across two records that nothing joins. Never call either without first running, in the same session, the query that would have found the thing.

---

## Configure each repository

Read **[configuring-a-repository.md](configuring-a-repository.md)**. It lists the required headings, says why each one cannot be derived or defaulted, and gives a filled-in example.

The rule that keeps this from growing back into a second protocol: **anything that is not one of those headings belongs in the skill, not in the config.**

---

## Install the skills

```bash
npx openskills install factoryengineering/skills
```

Install `night-shift-worker` and `refining-issues` into each tracked repository, and `night-shift-coordinator` into the repository the coordinator checks out.

`refining-issues` is what fills the queue the worker drains. It is installed beside the worker, in the same repository, because it reads the same `.night-shift/config.md` and audits that repository's issues — and because the bar the queue label asserts should be legible to anyone looking at the repository whose issues carry it. A person runs it; nothing schedules it. **Commit what you install.** A scheduled run that installs from the network at execution time can have its protocol changed by an upstream edit it never reviewed, and gains a failure mode at whatever hour it wakes.

Record which upstream commit each installed copy came from, so a later reader can tell an intentional local edit from an old copy.

---

## Create the sessions

Read **[creating-the-routines.md](creating-the-routines.md)**. Each session's prompt is a loader that names its skill and its configuration file, and nothing more. Protocol restated in a prompt is protocol that can disagree with the skill, and the disagreement will not surface until a run acts on the wrong half.

---

## Verify before you leave it running

1. Fire the coordinator by hand, in daylight, appending `Dry run. Sweep and report; dispatch nothing.` to the message.
2. Confirm it resolves the practice and every repository, reads each configuration file, and reports a queue per repository.
3. Let one scheduled run go unattended, then read the sweep back out of the log and check that its dispatch count matches its report.

## Resources

| Resource | Purpose |
|---|---|
| [creating-the-log.md](creating-the-log.md) | Publishing the seed, and the one-time practice and repository ceremony |
| [configuring-a-repository.md](configuring-a-repository.md) | The required headings, with a worked example |
| [creating-the-routines.md](creating-the-routines.md) | The scheduled sessions and their loader prompts |
| [log-model.factual](log-model.factual) | Read and send. The log's 47 definitions and its manifest, in four batches |
