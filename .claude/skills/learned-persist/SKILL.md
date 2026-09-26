---
name: learned-persist
description: Sweep the session for durable project learnings and write them to repo rules, skill overlays, or learned-general after the user approves each item.
disable-model-invocation: true
---

# Learned persist

Sweep this session for durable project learnings not yet written. Use the persist bar and destinations described below. Do not repeat work that was already written during the session.
Before writing anything give me an overview of the items you want to write, and give me the possibility to refuse some of them. Do NOT store the fact that I accepted or refused items.

These destinations are committed to the repo and shared with the team. Claude Code's auto-memory is personal and not committed; do not also save these items to auto-memory, and do not copy personal preferences from auto-memory into the repo.


## Bar

Persist **ONLY** facts that meet **ALL** of these:
- A future agent would likely fail or waste time without it
- It is not already obvious from code, tests, linters, rules, or existing skills
- It will still be true next week
Do not persist session recaps, speculation, secrets, temporary debug state, or restatements of an original skill. Write imperative instructions; update in place. If nothing durable was learned, write nothing.


## Destinations

Where to write:
- Correction or extension of an existing skill except `learned-general` → `.claude/overlays/<skill-name>/SKILL.md` as Replace / Add / Ignore patches, not a restatement. Same layout as a normal skill: `SKILL.md` plus optional `references/`. Delete empty sections; keep `SKILL.md` under ~80 lines. Put further detail in `references/` and link it from `SKILL.md` with when to read it. Do not move skill-specific detail into an always-on rule.
Never edit an existing skill except `learned-general`; create or update an overlay instead. Overlays are not invokable skills. Do not create an overlay for `learned-general`.
- Always-on convention or landmine → `.claude/rules/learned.md` (one or two lines each; keep the file under ~30 lines)
- Anything else that still meets the bar → `.claude/skills/learned-general/` (edit `SKILL.md` and optional `references/`)


# Optimize

Optimize stores only if needed:
   - `.claude/rules/learned.md`: keep only always-on conventions and landmines, one or two lines each; keep the file under ~30 lines. Move skill-specific facts to the matching overlay; move occasional facts to `learned-general`. Remove anything now in code or tests.
   - Each `.claude/overlays/<skill-name>/`: keep `SKILL.md` as Replace / Add / Ignore patches under ~80 lines. Move further detail into `references/` with when to read it. Delete empty sections. Remove restatements of the original skill and anything now in code, tests, or `learned.md`.
   - `.claude/skills/learned-general/`: keep `SKILL.md` under ~80 lines. Move detail into `references/` with when to read it. Delete empty sections. Remove anything duplicated in `learned.md`, overlays, code, or tests.


# Report

Report in less than 5 lines what you did.
If anything you learned is in conflict with CLAUDE.md or with existing rules, report each conflict as an additional line.
