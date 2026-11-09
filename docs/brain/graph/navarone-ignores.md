---
kind: information
subjects: [exclusion, process]
named-by: user
born: 2026-11-03
session: 7435de35-d06f-47ad-bf76-67b8cb922449
---
# Each repo's navarone.json holds ignore globs for files and projects, and what it ignores is gone from all of Navarone

navarone.json at the repo root holds, beside the limits, globs for files and for projects to ignore. Ignored code is gone from every part of Navarone: no check raises a signal on it, the lens does not show it, and it is no problem in reasons.json. Tests are the first use: we want beautiful code, and tests are a different kind of code.

Agents may change navarone.json, ignores included. Unlike a limit, which may never be set above its default, an ignore is trusted to the agent, and the user reverts a change by hand when it is wrong.

## Why
In the user's words: "each repo should get a navarone settings file (json) with in it ignore globs for files and projects to be ignored", "ignored means gone from all navarone tests", and "models are allowed to change the navarone file, I will trust them there and can always manually revert changes." Tests and generated code would otherwise flood the agents with signals on code nobody wants beautiful.

## Edges
- part-of -> process
