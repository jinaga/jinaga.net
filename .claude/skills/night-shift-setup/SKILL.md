---
name: night-shift-setup
description: Stands up the night-shift protocol for an organization, from nothing to a running nightly sweep. Creates the Night Shift Log application from a committed seed, or upgrades one published from an earlier seed, registers the practice and each tracked repository exactly once, writes the per-repository configuration file the worker refuses to run without, and creates the scheduled job that runs the sweep and the worker sessions it fires. Use when adopting the night-shift protocol for the first time, when adding a repository to an existing practice, when a log lacks the Night period that the coordinator and the morning review need, or when a run stopped because it found no practice or no repository matching the one it was sweeping.
metadata:
    github-path: skills/night-shift-setup
    github-pinned: b4f73c29cceae5740c06359e0788c8c40db91d59
    github-ref: b4f73c29cceae5740c06359e0788c8c40db91d59
    github-repo: https://github.com/factoryengineering/skills
    github-tree-sha: ea2b09aaaf3cdece01da3fb7ca0589e21cb79e13
---

# Night Shift Setup

The night shift is a coordinator that sweeps a labelled issue queue and one worker per repository that takes a single issue to a pull request. Neither keeps state between runs, so both read a log, and the log is an application you create once and share across every repository you track.

This skill covers the one-time acts. `night-shift-coordinator` and `night-shift-worker` cover everything that happens nightly, `night-shift-review` covers the morning, and `refining-issues` covers the issues that go into the queue.

## When to use

- **Adopting the protocol** for the first time in an organization.
- **Adding a repository** to a practice that already exists.
- **A run stopped** because it found no practice, no matching repository, or more than one of either.
- **A run stopped** because a repository's configuration file was missing a required heading.
- **The log predates the Night period**: `describe startNightlySweep` finds nothing, so the coordinator cannot file a sweep under its night and the morning review has nowhere to start.

---

## What you are building

| Piece | Where it lives | How many |
|---|---|---|
| The Night Shift Log application | your Factual workspace | one per organization |
| A practice, and one repository record per tracked repository | inside that application | one practice, one record each |
| `.night-shift/config.md` | each tracked repository | one per repository |
| `.night-shift/routines.md` | the coordinator's own repository | one |
| The installed skills | committed into each repository | worker and `refining-issues` in each tracked repository; coordinator and `night-shift-review` in the coordinator's own |
| The worker sessions | your agent host, as routines | one per repository |
| The coordinator's scheduled job | a CI runner or a server outside the agent host | one |

```
Task Progress:
- [ ] Create the log application and publish the seed
- [ ] Create the practice, exactly once
- [ ] Register each tracked repository, exactly once
- [ ] Write .night-shift/config.md in each tracked repository
- [ ] Install and commit night-shift-worker and refining-issues in each tracked repository
- [ ] Install and commit night-shift-coordinator and night-shift-review in the coordinator's repository
- [ ] Write .night-shift/routines.md
- [ ] Create the worker sessions and the coordinator's job, and run the coordinator once by hand
```

---

## Create the log

Read **[creating-the-log.md](creating-the-log.md)**. It publishes `log-model.factual` into a new application, then runs the practice and repository ceremony. **A log published from an earlier seed needs the upgrade** in that file before the coordinator can file a sweep under its night; the upgrade is additive and rewrites nothing recorded.

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

Install `night-shift-worker` and `refining-issues` into each tracked repository, and `night-shift-coordinator` and `night-shift-review` into the repository the coordinator checks out.

`night-shift-review` is what a person runs each morning to read the night's results and decide what merges. It lives beside the coordinator because it reads the same `.night-shift/routines.md` to find every tracked repository, and because a morning spans all of them. Like `refining-issues`, nothing schedules it.

`refining-issues` is what fills the queue the worker drains. It is installed beside the worker, in the same repository, because it reads the same `.night-shift/config.md` and audits that repository's issues — and because the bar the queue label asserts should be legible to anyone looking at the repository whose issues carry it. A person runs it; nothing schedules it. **Commit what you install.** A scheduled run that installs from the network at execution time can have its protocol changed by an upstream edit it never reviewed, and gains a failure mode at whatever hour it wakes.

Record which upstream commit each installed copy came from, so a later reader can tell an intentional local edit from an old copy.

---

## Create the sessions

Read **[creating-the-routines.md](creating-the-routines.md)**. **The coordinator cannot be a routine**: the fire endpoint refuses a routine token used from inside a Claude Code cloud session, so the coordinator runs as a scheduled job outside one. Each session's prompt is a loader that names its skill and its configuration file, and nothing more. Protocol restated in a prompt is protocol that can disagree with the skill, and the disagreement will not surface until a run acts on the wrong half.

---

## Verify before you leave it running

1. Start the coordinator's job by hand, in daylight, with `Dry run. Sweep and report; dispatch nothing.` appended to its prompt.
2. Confirm it resolves the practice and every repository, reads each configuration file, and reports a queue per repository.
3. Let one scheduled run go unattended, then read the sweep back out of the log and check that its dispatch count matches its report.
4. The next morning, run `night-shift-review`. Confirm `nightsInPractice` lists the night and `sweepsInNight` lists one sweep per repository. A night that is missing means the coordinator still calls `startSweep`, so check that its installed copy is current.

## Resources

| Resource | Purpose |
|---|---|
| [creating-the-log.md](creating-the-log.md) | Publishing the seed, and the one-time practice and repository ceremony |
| [configuring-a-repository.md](configuring-a-repository.md) | The required headings, with a worked example |
| [creating-the-routines.md](creating-the-routines.md) | The coordinator's job, the worker sessions, and their loader prompts |
| [log-model.factual](log-model.factual) | Read and send. The log's 53 definitions and its manifest, in four batches |
