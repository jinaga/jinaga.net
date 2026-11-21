---
name: tracking-jinaga-spec
description: >
  Use when bringing jinaga.net up to date with jinaga/jinaga-spec: re-pinning
  the conformance vectors in Jinaga.Test/Conformance/Vectors, triaging the
  vectors that then fail, and porting a proven algorithm (split, well-formedness,
  inverses, given-condition reduction, feeds) from the spec's Lean definition and
  TypeScript reference port instead of patching until the tests pass. Also use
  when an issue cites a jinaga-spec PR, theorem or vector.
---

# Tracking jinaga-spec

[jinaga/jinaga-spec](https://github.com/jinaga/jinaga-spec) defines Jinaga's
specification algorithms in Lean, proves them, and generates conformance
vectors from the definitions. This repository tests against a pinned copy of
those vectors (`Jinaga.Test/Conformance/README.md` names the commit and the
procedure). A fix here should be a port of the proven algorithm, not a patch
that makes a test pass: patches found by trial and error have been shown to fall
short before (jinaga.js#338).

## 1. See what changed since the pin

From a sibling checkout of jinaga-spec:

```bash
PIN=$(grep -oE '[0-9a-f]{40}' ../jinaga.net/Jinaga.Test/Conformance/README.md | head -1)
git -C ../jinaga-spec log --oneline $PIN..origin/main -- vectors JinagaSpec docs/findings.md
git -C ../jinaga-spec diff --stat $PIN origin/main -- vectors
```

Read the PR descriptions of the merged commits (`gh pr list -R jinaga/jinaga-spec
--state merged --search <sha>`). Each one says which theorem changed and what a
port has to take.

## 2. Re-pin

Follow "Updating the pin" in `Jinaga.Test/Conformance/README.md`: copy each
pinned directory over its namesake, deleting files that no longer exist, and
replace the commit.

- Pin only the directories this library has a test for. `vectors/feeds/` and
  `vectors/cascade/` exercise code that lives elsewhere (feeds are built by the
  replicator, and `Network.Feeds` only asks for them), so don't pin them unless
  this library starts building feeds or running pipelines.
- A vector with a field the loader (`ConformanceVector.cs`) doesn't understand
  should fail loudly, not be ignored. Extend the loader, and say in the README
  which test reads the field.
- Never edit a vector by hand.

## 3. Triage each failure

Run `dotnet test Jinaga.Test --filter FullyQualifiedName~Conformance` and sort
every failing vector into one of:

| Kind | How to tell | What to do |
|---|---|---|
| Missing port | The vector exercises a shape or rule added since the pin (child specifications, given conditions, a new well-formedness rule) | Port it (step 4) |
| Bug | The shape was covered before, and jinaga.js agrees with the spec on it | Port the fix (step 4), with the vector as the red test |
| Spec question | jinaga.js and the spec disagree, and this library agrees with jinaga.js | Read `docs/findings.md` in jinaga-spec: it usually says which one is wrong and why. If it doesn't, ask before choosing |

Write the triage down in the PR description, one line per vector.

## 4. Port from the definition

For each algorithm, read three things in jinaga-spec, in this order:

1. The Lean definition (`JinagaSpec/Split.lean`, `WellFormed.lean`,
   `Candidates.lean`/`Reorder.lean`, `GivenConditions.lean`, `Feeds.lean`).
   It's written to be readable on its own.
2. The theorem it satisfies, and its hypotheses (`JinagaSpec/Proofs/`). Each
   hypothesis on the input is something the port must reject, or must say it
   assumes. jinaga-spec gives each one a check (`isWellFormed`,
   `isWellFormedDeep`). Port the check together with the algorithm.
3. The TypeScript reference port in `ports/typescript/`. It mirrors the Lean
   line for line, and is the nearest thing to C# you can copy from.

Port the definition's structure, so a reader can check the C# against the Lean.
If C# idioms force a different shape, say in a comment which Lean function each
method corresponds to.

## 5. Close the loop

- Update the pin and the table in `Jinaga.Test/Conformance/README.md`.
- In the PR, link the jinaga-spec PRs and theorems the port follows, and any
  jinaga.js issue for the same bug.
- If a vector exposes something the spec gets wrong, or a shape no vector
  covers, open an issue against jinaga-spec instead of working around it here.
