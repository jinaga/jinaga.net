# Creating the scheduled sessions

The night shift runs one coordinator on a schedule, and one worker per tracked repository that the coordinator fires on demand. Each worker is a scheduled Claude Code session (a routine). **The coordinator runs outside a Claude session**, as a scheduled job on a CI runner or a server of your own, running Claude Code headless.

## Why the coordinator is not a routine

The coordinator fires each worker through the routine fire endpoint, using that worker's token. **That endpoint refuses a routine token used from inside a Claude Code cloud session.** As of 2026-10-31, such a call returns:

```
403 permission_error
This routine can't be fired by its API token from a sandbox with restricted network access. Fire it from outside the sandbox.
```

The refusal is deliberate. The request reaches the API and comes back with a structured error and a request id, so it is not a network-policy problem in the session's environment, and no environment setting lifts it. Anthropic documents the fire endpoint for callers such as "alerting systems, CI pipelines, and internal tools", and documents no way for one session to fire another. A coordinator running as a routine still sweeps and records every night, and dispatches nothing.

**Do not route around it by switching off the session's sandbox.** The error text invites exactly that. A scheduled session that removes a safety control because an error message suggested it is a worse defect than the one it fixes.

**Do not relay a cloud session's fire through a CI job or any other caller.** A relay obeys the error text literally, because the fire happens outside the sandbox, and still triggers exactly the fire the platform refused. The refusal exists so that a session cannot start sessions, so the test is what a session's output controls.
- A session's output may be an event that a caller outside the session reacts to by its own fixed rule.
- It must never choose what that caller fires: not the routine, not the issue, not the message. Nor may it change how many fires happen.
- A caller that takes the target, the message or the number of its fires from a session's output is a relay, however the output travels.

The cases:
- **A workflow on `pull_request: opened` that fires a measurement routine:** allowed. The workflow's own rule gives every pull request one fire, and a worker opens one pull request.
- **An issue comment that a workflow turns into a fire:** a relay. The comment carries the dispatch.
- **A label that starts a routine on an issue the session chose:** a relay. The label chooses the issue.
- **A routine that starts the coordinator's job:** a relay. Each run of the job is a sweep with a full budget of its own, so a session that can start the job adds fires each time it does. Start the job from a timer outside any session instead (*When the job starts*, below).

Move the process that decides what to fire outside the session instead, as this file does for the coordinator.

The refusal applies to anything else a cloud session fires: a worker whose repository has it fire another routine after its pull request, for instance. The refusal was observed on coordinator sessions, and nothing distinguishes a worker's session from one. There are two remedies:

1. **Do the work inside the session, and fire nothing.** Run it in a subagent whose definition narrows its tools to what the work needs. The credential the work needs comes from the session's own environment, never from the repository. Prefer this wherever the work fits the session's environment, time and credentials: it adds no component, and the result lands in the session, which can act on it before it stops.

   **That credential reaches every process the session runs, not only the subagent.** A cloud environment's variables become ordinary environment variables that any command in the session can read. As of 2026-11-08, a subagent's definition has no way to hold a credential of its own. A network secret keeps the value off the session's machine, but the proxy adds it to every request to its host, so it does not stop a test from making a paid call either. Before you add a credential to a worker's environment:
   - **Find the code that would pick it up without being asked**, such as tests, scripts, and the commands under `## Verification commands`.
   - **Make that code require an explicit opt-in**: a flag the subagent passes on its own command line. The credential's presence alone must trigger nothing.
   - **Unset it in the verification commands themselves**, as `configuring-a-repository.md` requires, so a test that misses the opt-in still cannot reach it.

   Where the work cannot be made to opt in, take remedy 2, with a routine and environment of its own, so the worker's environment never holds the credential.
2. **Move the fan-out to a caller outside the session** when the work needs a different checkout, environment or schedule, or must outlive the session. That caller must fire by its own fixed rule, such as one fire per pull request opened, or it is the relay forbidden above.

**A coordinator routine cannot dispatch, so it is not a rollback.** Keeping it disabled buys nothing: re-enabling it does not restore dispatching, because the refusal applies to it as before. It only sweeps and records alongside the job, from a prompt nothing keeps in step. When the coordinator moves to a scheduled job, delete the routine, or clear its schedule and its tokens. The job's rollback is the revert described under *The coordinator's job*.

## Where each session runs

Each worker checks out the repository it works, because it builds, tests and pushes there.

The coordinator only reads and fires, so it needs no checkout of a tracked repository. Run it from the repository that holds its own configuration, rather than borrowing a tracked one. A coordinator that checks out a tracked repository reads that repository's copy of everything as if it were authoritative for all of them.

## The routine table

The coordinator's repository carries `.night-shift/routines.md`. It has one row per session, with:
- the session identifier
- its role
- its prompt file
- the repository it checks out
- the repository it targets
- the name of its token
- its schedule

The coordinator's row names the job that runs it in place of a routine identifier, and **where the job publishes the coordinator's final report**, such as the workflow whose run summary carries it. `night-shift-review` reads that report each morning, and it is the only place a forked chain, a repeated no-op, a failed fire or an unrecorded dispatch reaches a person. A report published somewhere the review has to guess at is a report nobody reads.

One table, not two. The coordinator reads the worker rows to know what to fire; a person reads every row to know what exists. Storing a session identifier in two places is how the two fall out of step. A job that needs the list of tracked repositories, to scope a credential for example, should read it from the worker rows rather than keep its own.

The token *names* belong in the table. The tokens themselves belong in the coordinator host's secret store, exposed to the coordinator as environment variables of those names, and never in a repository.

## Prompts are loaders

A prompt names the skill and the configuration, and stops. Everything else is protocol, and protocol restated in a prompt is protocol that can disagree with the skill it duplicates. The disagreement will not surface until a run acts on the wrong half.

One worker prompt serves every worker session. Nothing in it is repository-specific: the checkout already names the repository, and everything that varies is in that repository's `.night-shift/config.md`.

**Coordinator:**

```
You are the night-shift coordinator.

Read `.night-shift/routines.md` in this checkout. It lists the repositories to
sweep, the session to fire for each, and the environment variable holding that
session's token.

Then follow the night-shift-coordinator skill exactly. That skill is the
protocol. This prompt is not: where the two disagree, the skill wins, and say
so in your report.

Budget: <n> dispatches per night, across all repositories.
```

**Worker:**

```
You are a night-shift worker.

The message you were fired with names one issue in this repository, and may
name a lower issue to stack on.

Read `.night-shift/config.md` in this checkout, then follow the
night-shift-worker skill exactly. That skill is the protocol. This prompt is
not: where the two disagree, the skill wins, and say so in your report.
```

The budget belongs in the coordinator's prompt rather than in the skill, because it is an operator's choice about one night's capacity and not a property of the protocol. The skill's part is to require that the number be stated in the report and wherever a skip leans on it (`night-shift-coordinator`, under **How much to take**), so the prompt gives the number and its scope, and nothing else.

**State the budget per night, never per sweep.** The log records one sweep per repository, so "per sweep" can be read as a separate budget for each repository. A coordinator that reads it that way can dispatch several times what you meant.

## The coordinator's job

The job runs the coordinator prompt through headless Claude Code (`claude -p`) on the schedule, with the coordinator's repository checked out. It reads the prompt file from the default branch on every run, so merging a prompt change deploys it, and reverting the commit rolls it back.

**Pin what the job installs.** Install a fixed Claude Code version, never the latest, and pin each MCP server image by tag or digest. The reason is the one under **Commit what you install** in `night-shift-setup/SKILL.md`: a CLI release can change permission handling or output, and a server release can rename its tools.
- **The pin in the job's definition is the only record of those versions.** Do not list them again beside the skills' upstream commits. A second list is a copy that can drift from the pin.
- **An upgrade is a change of its own**, made after reading the release notes, as with a skill update.

**Never let two runs of the job overlap.** The coordinator skill forbids dispatching one issue twice in a sweep, but two sweeps at once each read the queue before the other records a dispatch, find the same issues available, and fire them all. A run started by hand while the scheduled one is still going does this, and so does a scheduled run that starts late or runs long.
- **Queue a second run; do not cancel the first.** A run that is cancelled partway through firing workers leaves dispatches made and unrecorded.
- **Set a timeout on the job**, so a stuck run releases the group and the queued run can start.

On GitHub Actions:

```yaml
concurrency:
  group: night-shift-coordinator
  cancel-in-progress: false

jobs:
  coordinator:
    timeout-minutes: 60
```

It needs four credentials. The first three are the coordinator's. The fourth belongs to the job's own steps, and the coordinator session never sees it.

**A read-only hosting credential covering every tracked repository.**
- On GitHub, use a GitHub App installed on the tracked repositories and the coordinator's own. Give it read-only access to contents, issues, pull requests and metadata, and mint an installation token for each run.
- A token scoped to the repository the job runs in reaches none of the others, so it is not enough.
- A read-only token turns the coordinator's hard limits into the host's: a push, a merge, or a label edit is refused, not merely forbidden.

**A credential for the Night Shift Log.** Pass it to the log's MCP server as an `Authorization: Bearer` header in the job's MCP configuration. `app.jinaga.com` has no sign-in that works without a person, so the token comes from an interactive sign-in:
- **Sign in with the same upstream account the log's practice belongs to.** The log finds the practice through the identity the token carries, and a different account finds nothing.
- **Ask for `offline_access`**, as `mcp-remote --authorize-param "scope=openid profile email offline_access"` does. Run the sign-in with its own configuration directory, so it does not rotate a sign-in that another client on the same machine relies on.
- **Store the access token, not a refresh chain.** The access token lasts 30 days. A refresh chain cannot outlast that by much:
  - A refresh token stops working 14 days after the sign-in that issued it, because refreshing never extends the grant behind it.
  - Presenting a used refresh token again revokes the whole sign-in.

  So a chain of refreshes buys a few extra days, at the cost of a write-back step that breaks the sign-in if it ever fails.
- **Renew it by hand each month**, and store its expiry beside it so the job can warn a week ahead.

**Each worker's fire token**, under the names in the routine table.

**A credential that can open issues in the coordinator's own repository, for the job's notices.** The job opens an issue when the log is unreachable (*Raising an unreachable log*, below) and when a night passes with no sweep (*When the job starts*, below), and the coordinator's own credential is read-only, so it cannot.
- Scope it to issue writes in the coordinator's repository and nothing else.
- Use it only in the job's own steps, outside the coordinator session. The coordinator keeps only the read-only credential, so its hard limits stay enforced by the host.
- On GitHub Actions, this is the workflow's built-in token with `issues: write`. Pass it only to the step that opens the issue, and check the repository out with `persist-credentials: false`. Otherwise the checkout stores the token in `.git/config`, where the coordinator's shell can read it.

```yaml
permissions:
  contents: read
  issues: write

steps:
  - uses: actions/checkout@v4
    with:
      persist-credentials: false
  - name: Open the outage notice
    if: steps.preflight.outputs.log_unreachable == 'true'
    env:
      GH_TOKEN: ${{ github.token }}
    run: gh issue create --title "Night shift: log unreachable" --body "..."
```

Allow the coordinator only the tools it reads and fires with, through headless Claude Code's allowed-tools list: the hosting tools in read-only mode, the log's tools, and a shell for the fire call. It has no repositories to attach, because its credential already reads all of them.

## When the job starts

**On GitHub Actions, the `schedule` trigger starts runs hours late, and some nights not at all.** Evidence from jinaga/night-shift's coordinator, 1 to 8 November 2026:
- A run due at 04:07 UTC was created between 11:12 and 12:15 UTC on six nights, and not at all on the seventh.
- Changing the cron and its time zone moved nothing. A run due at 05:07 UTC arrived at 11:51.
- Each run started in the same second it was created. The delay comes before any runner is chosen, so a self-hosted runner or a larger plan does not change it.
- A run started through `workflow_dispatch` begins within seconds.

GitHub documents only that scheduled runs "can be delayed during periods of high loads" and that queued jobs "may be dropped". Other users report the same pattern: [community #201738](https://github.com/orgs/community/discussions/201738), [#210888](https://github.com/orgs/community/discussions/210888), [#210625](https://github.com/orgs/community/discussions/210625).

**Decide how late a sweep may start.** A team that reads the night's pull requests at 9 AM may not mind a sweep at 6 AM. A team whose workers need the whole night does. That is your call, and it decides the setup.

Whatever the host, the setup must:
- **Fire from outside a Claude session**, as *Why the coordinator is not a routine* requires.
- **Start within the tolerance you chose.**
- **Keep to the local time you chose across daylight-saving changes.** A scheduler that runs on UTC only needs its cron edited twice a year, and the sweep moves an hour whenever someone forgets.
- **Make a repeated sweep harmless.** A timer you can trust delivers at least once, so it can deliver twice. A backup schedule runs a second time on purpose, and a person starts a recovery run by hand. The worker's claim (*Claim the issue* in `night-shift-worker`) makes a second worker on a claimed issue stand down. A second sweep still takes a full budget of its own. Most of it lands on issues already claimed, whose workers stand down, but nothing guarantees that. So keep second runs rare, as the backup guard below does, and treat a recovery run as a person's decision about capacity.
- **Notice a night with no sweep, through something other than the scheduler that missed it.** A dropped scheduled run fails nothing and alerts no one.

| Tolerance | Setup |
|---|---|
| Hours, and an occasional missed night | GitHub Actions `schedule` alone. It is free and needs no other service, but it still needs the missed-night check. |
| Minutes | GitHub Actions, started by a timer outside GitHub through `workflow_dispatch`, with `schedule` kept as a backup. |
| Minutes, without GitHub Actions | A host with a scheduler of its own, such as a server with a systemd timer (`OnCalendar=*-*-* 23:07:00 America/Chicago`, `Persistent=true`). The job definition changes, but the credential and tool rules in this file carry over. |

**Choose an outside timer by what it does, not by name.** It must support IANA time zones, retry a failed delivery, and send an authenticated `POST` to the workflow's `dispatches` endpoint. As of 2026-11-08, Google Cloud Scheduler does all three. Services change, so check a timer against these three before you rely on it. Give its token only Actions write access on the coordinator's repository.

**An outside timer is not a relay.** No session is involved, so the relay rule under *Why the coordinator is not a routine* does not reach it. It fires no routine either: it starts the job, and the job decides what to fire. A routine used as the timer is a different case, and that section rules it out.

**Skip a backup run when tonight's sweep already ran.** With `schedule` as a backup, every night has two runs: the timer's, and the late scheduled one. Before the sweep, a run whose `event` is `schedule` reads this workflow's own run history. It exits without sweeping if another run in the last 16 hours is in progress or succeeded. Sixteen hours is longer than any delay observed above, and shorter than the gap back to the previous night's run.
- **It reads the host's record of runs, not the log**, so the log still decides nothing about availability.
- **It never skips a run started by hand or by the timer**, because their `event` is `workflow_dispatch`. A recovery run always sweeps.
- **A failed run does not count**, because a failure may have come before the sweep. That includes a night the log was unreachable, which fails its run on purpose. The backup then sweeps again, and the claim makes that harmless.

```yaml
on:
  schedule:
    - cron: '7 23 * * *'
      timezone: America/Chicago
  workflow_dispatch:

permissions:
  actions: read
  contents: read
  issues: write

steps:
  - name: Skip a backup run when tonight's sweep already ran
    id: backup
    if: github.event_name == 'schedule'
    env:
      GH_TOKEN: ${{ github.token }}
    run: |
      since=$(date -u -d '16 hours ago' +%Y-%m-%dT%H:%M:%SZ)
      ran=$(gh run list --repo "$GITHUB_REPOSITORY" --workflow coordinator.yml \
        --limit 20 --json databaseId,createdAt,status,conclusion \
        --jq "[.[] | select(.databaseId != $GITHUB_RUN_ID and .createdAt >= \"$since\"
               and (.status != \"completed\" or .conclusion == \"success\"))] | length")
      if [ "$ran" -gt 0 ]; then echo "skip=true" >> "$GITHUB_OUTPUT"; fi
  # Condition every later step on: steps.backup.outputs.skip != 'true'
```

**Check for a missed night from outside the scheduler that missed it.**
- **A heartbeat monitor** catches a miss the morning it happens. The job pings it at the end of every sweep, and the monitor alerts a person when no ping arrives by a deadline. Prefer it where a missed night matters.
- **The next run's pre-flight** can look back for the previous night's run and, finding none, open an issue with the job's notice credential. It needs no other service, but it reports a miss a day late, and reports nothing if runs stop altogether. GitHub disables a scheduled workflow in a public repository after 60 days without activity.

## Raising an unreachable log

The coordinator skill requires an unreachable log to be raised where a person sees it the same day, before the first dispatch. A Claude session does that with its push-notification tool. A coordinator outside a Claude session has no such tool, so the job raises it instead:

1. **Before the sweep, ask the log's server whether the credential works.** Send its MCP endpoint an `initialize` request with the token. Anything but `200` means unreachable: `401` is an expired or revoked sign-in, and anything else is an outage. Do not judge by the stored expiry alone, or a revoked sign-in passes as a valid one.
2. **If it is unreachable, leave the log's server out of the coordinator's MCP configuration.** The coordinator then finds its tools absent, which is the skill's own definition of unreachable, and does the hosting-platform work anyway.
3. **Open an issue in the coordinator's repository before the coordinator runs**, so the notice exists before any dispatch. Use the job's notice credential, never one the coordinator holds.
4. **Fail the job at the end**, so the host's own failure alert reaches a person.

Publish the coordinator's final report somewhere a person reads, such as the run's summary page, and name that place in the coordinator's row of `.night-shift/routines.md` (*The routine table*, above). On a night the log was unreachable, that report holds the only list of the night's dispatches.

Workers remain Claude sessions, and need the push-notification tool on their allowed-tools list as before. A session sends that notification itself, so it does not depend on the routine's own completion-notification setting. **That last point is read from the tool contracts and has not been confirmed against a live run**, so confirm it on a worker's first run.

## Tool access for workers

A worker needs the hosting API for issues and pull requests, the Factual MCP server, and permission to run `register-stack.sh` at its installed path. **When the script's path changes, the worker's allowed-tools list changes in the same edit.** A path that no longer matches does not error; the call is simply refused at whatever hour the run wakes, and the chain is silently left unregistered.

## Keeping worker prompts and files in step

The worker prompt file lives in the coordinator's repository; the live prompts live in the routine API. They drift the moment one is edited without the other. The coordinator's prompt has no live copy, so this does not apply to it.

Sync in one direction only, from the files to the sessions, and make it a reviewed step rather than something a run does to itself:

1. Read the live prompt.
2. Archive it verbatim if no archived copy exists yet. That copy is the rollback.
3. Compare against the file. Identical means nothing to do.
4. Write the file's contents, with the tool list and the checkout, in one update.
5. Read it back and confirm it matches. **If it does not, report the difference and stop. Never retry a write.**

## First run

Start the coordinator's job by hand, in daylight, with `Dry run. Sweep and report; dispatch nothing.` appended to its prompt. Confirm it resolves every repository and reports a queue for each. Only then let the schedule run it unattended.

**Confirm the unreachable-log path on a second dry run, with the log's token withheld.** An issue should open before the sweep and the job should fail at the end. If neither happens, the unreachable-log rule in the coordinator's `night-shift-log.md` has no way to reach anyone, and the job needs fixing before the schedule takes over.
