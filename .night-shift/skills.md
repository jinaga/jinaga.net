# Installed skills

These skills are installed here as committed copies. They are not fetched at run
time: an upstream edit would otherwise change what an unattended run does
without anyone reviewing it, and a network install adds a failure mode at
whatever hour the run wakes.

| Skill | Source | Upstream commit |
|---|---|---|
| `night-shift-setup` | `factoryengineering/skills` | `2fb85d8297c0a3059365bd809b8242f5657f2740` |
| `night-shift-worker` | `factoryengineering/skills` | `2fb85d8297c0a3059365bd809b8242f5657f2740` |
| `refining-issues` | `factoryengineering/skills` | `9d44b2422f9704e49f13ee2cc61b9a0f0737b07d` |
| `degrees-of-freedom` | `michaellperry/skills` | `b1dbdfe374847d5a595b0f2a0066feb684fc5219` |

The worker, `refining-issues`, and `degrees-of-freedom` commits are those
recorded by `jinaga.js`, whose installed copies these are identical to (apart
from the `.openskills.json` install marker). `night-shift-setup` was installed
in the same pass as the worker; its commit is assumed to be the worker's and has
not been verified against upstream.

To take an upstream change, re-copy the skill directory from its source, update
its commit above, and read the diff before you push. Local edits to an installed
skill belong upstream instead, or the next update silently reverts them.
