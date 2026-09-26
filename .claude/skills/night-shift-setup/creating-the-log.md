# Creating the Night Shift Log

## Contents

- [Publishing the seed](#publishing-the-seed)
- [The practice ceremony](#the-practice-ceremony)
- [Registering a repository](#registering-a-repository)
- [References are session tokens, not identifiers](#references-are-session-tokens-not-identifiers)

## Publishing the seed

The log is a Jinaga application reached through the Factual MCP server. Create it once per organization.

1. Open a console. It starts unbound.
2. `let $apps = applications`. The response lists every workspace you can reach, each carrying its own `ref`. Pick the workspace you want and take that `ref`. Spend it in a **second** call, because a batch that binds and indexes in one go is guessing at an index it has not seen yet.
3. `create application "Night Shift Log" in <the ref you took>`, on a clean console.

   The indexes are one flat sequence across workspaces and the applications inside them, so the workspace you want is not reliably `$apps[0]`. Read the ref off the response rather than assuming a position.
4. Send `log-model.factual` in its four marked batches, **in file order**. Do not reorder: fact types precede the specifications that walk them, specifications precede the actions that write them, and within the fact types each one follows every type it names as a predecessor. Sending the batches separately makes a failure name its layer, because a batch stops at its first failure.
5. `publish` after batch 3. Expect a `manifest_missing` warning; batch 4 is the answer to it.
6. Send batch 4, then publish again.

**Batch 4 is not decoration.** It carries the routing that lets anything discover the application, and the per-action argument guidance that both night-shift skills tell a run to read instead of trusting a list in a file. An application published without it works and is undiscoverable.

## The practice ceremony

A **practice** is one person's night-shift setup, and one practice covers every repository tracked. It roots at a single-use owner, so you reach it through `practicesForAdministrator` rather than by owning it.

```
query practicesForAdministrator($me)
```

| Rows | What it means | What to do |
|---|---|---|
| exactly one | Already set up | Use it. **Do not call `createPractice`.** |
| none | First time | `call createPractice($me)`, then re-run and confirm exactly one. |
| more than one | Setup ran twice | **Stop.** Two practices split the history. Ask which to keep; `deletePractice` retires the other. |

## Registering a repository

Inside the practice, find the repository whose current name matches the one you are adding.

```
query repositoriesInPractice($practice)
```

| Rows matching the name | What to do |
|---|---|
| exactly one | Already registered. **Do not call `registerGitHubRepository`.** |
| none | Read the numeric repository id from the host, then `call registerGitHubRepository($practice, $githubId, "owner/name")`. Re-run and confirm exactly one. |
| more than one | **Stop.** Two bindings for one repository split the history the same way two practices do. |

The numeric id is what survives a rename or a transfer. The name is a mutable property superseded through `prior`, which is why a rename uses `renameRepository` and never edits anything.

**`createPractice` and `registerGitHubRepository` are the only non-idempotent writes in this system.** Neither may ever sit in a retry or a fallback position. A shell `cmd-a || cmd-b` runs `cmd-b` when `cmd-a` merely prints something unexpected, and that second call is a real write, not a test.

## References are session tokens, not identifiers

A `practiceRef`, a `repositoryRef` and a `nameRef` are scoped to the console that produced them. **Never write one into a configuration file.** Store the application title and the repository name, and re-resolve the references through `practicesForAdministrator` at the start of every run. A stored reference is a value derived from something else, and it goes stale silently.
