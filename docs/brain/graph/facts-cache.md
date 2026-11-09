---
kind: information
subjects: [facts-cache, lens, performance]
named-by: user
born: 2026-11-06
session: 7f981c5a-e9b7-4d08-806b-4cc58574a310
---
# Navarone caches the codebase facts per code version in a gitignored .navarone folder

Every command that reads the code, check and lens alike, gets its facts through one seam, and that seam keeps them in `.navarone/` at the repo's root. An entry is keyed by the HEAD commit, a hash of every file that differs from it, navarone.json and navarone's own version, so a builder's dirty worktree still hits while its code stays the same. The folder keeps the newest 10 entries and prunes before it writes. `navarone init` puts `.navarone/` in the repo's `.gitignore`.

## Why
In the user's words: "parsing the entire system every query is horribly inefficient." The facts depend only on the code, the ignores and the tool that built them, so the same version never needs parsing twice.

## Edges
- part-of -> lens-into-the-code
- uses -> navarone-init
- uses -> navarone-ignores
