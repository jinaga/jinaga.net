# Creating the scheduled sessions

The night shift runs as scheduled Claude Code sessions: one coordinator on a cron schedule, and one worker per tracked repository that the coordinator fires on demand.

Each worker checks out the repository it works, because it builds, tests and pushes there. The coordinator's checkout is free, since it only reads and fires, so give it the repository that holds its own configuration rather than borrowing one of the tracked repositories. A coordinator that checks out a tracked repository reads that repository's copy of everything as if it were authoritative for all of them.

## The routine table

The coordinator's repository carries `.night-shift/routines.md`: one row per session, with the session identifier, its role, its prompt file, the repository it checks out, the repository it targets, its token's environment variable name, and its schedule.

One table, not two. The coordinator reads the worker rows to know what to fire; a person reads every row to know what exists. Storing a session identifier in two places is how the two fall out of step.

The token *names* belong in the table. The tokens themselves belong in the session environment, never in a repository.

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

Budget: <n> dispatches per sweep. State this number in every skip rationale
that leans on it, and in your report.

Do not poll the workers. End your turn once every fire has returned and been
recorded, and report what you dispatched, what you skipped and why.
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

The budget belongs in the coordinator's prompt rather than in the skill, because it is an operator's choice about one night's capacity and not a property of the protocol. The skill's part is to require that the number be stated wherever a skip leans on it.

## Tool access

The coordinator needs to read issues, pull requests and branches across every tracked repository, and to attach the repositories it does not check out. It needs the Factual MCP server for the log, and the ability to fire the worker sessions. It needs nothing that writes to a repository.

A worker needs the hosting API for issues and pull requests, the Factual MCP server, and permission to run `register-stack.sh` at its installed path. **When the script's path changes, the worker's allowed-tools list changes in the same edit.** A path that no longer matches does not error; the call is simply refused at whatever hour the run wakes, and the chain is silently left unregistered.

**Both roles need the harness's push-notification tool on their allowed-tools list.** A session sends that notification itself, so it does not depend on the routine's own completion-notification setting: the routine setting reports that a run finished, and the tool reports something from inside the run. They are separate mechanisms, and a routine whose completion notifications are off should still deliver one a session sends. **That last point is read from the tool contracts and has not been confirmed against a live run**, so confirm it on the first run below before you rely on it; a run whose only way of reporting an unreachable log is a notification that never arrives reports nothing at all.

## Keeping prompts and files in step

The prompt files live in the coordinator's repository; the live prompts live in the routine API. They drift the moment one is edited without the other.

Sync in one direction only, from the files to the sessions, and make it a reviewed step rather than something a run does to itself:

1. Read the live prompt.
2. Archive it verbatim if no archived copy exists yet. That copy is the rollback.
3. Compare against the file. Identical means nothing to do.
4. Write the file's contents, with the tool list and the checkout, in one update.
5. Read it back and confirm it matches. **If it does not, report the difference and stop. Never retry a write.**

## First run

Fire the coordinator by hand, in daylight, appending `Dry run. Sweep and report; dispatch nothing.` Confirm it resolves every repository and reports a queue for each. Only then let the schedule run it unattended.

**Confirm the push notification on that same run**, by appending `Send a push notification saying the dry run started.` and watching for it to arrive. Do it with the routine's own completion notifications set the way you intend to leave them, because that is the combination the night shift will actually run in. If nothing arrives, the unreachable-log rule in `night-shift-log.md` has no way to reach anyone, and the tool list or the routine's settings need fixing before the schedule takes over.
