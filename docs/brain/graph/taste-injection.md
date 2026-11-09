---
kind: information
subjects: [taste, review, agents]
named-by: user
born: 2026-11-04
---
# The user's taste lives in NAVARONE.md: a central one that Navarone installs for every agent, and a local one per repo with extra rules

NAVARONE.md replaces taste.md. Navarone's install.sh puts the central NAVARONE.md in ~/.claude/rules, so every agent loads it. It holds the user's taste and the process: in a repo with a navarone.json, the builder runs Navarone and answers every signal, by fixing the code or recording a resolution, and the validator runs it and reviews the resolutions with the user's taste.

A repo may have its own NAVARONE.md with extra taste rules for that project, the way a project's BUILDER.md extends the central builder role. The central NAVARONE.md tells the agent to read it when it exists. Where a local rule contradicts the central taste, the local one wins in that repo: the `++at` trick in CkLang's lexer is fine there and nowhere else. No repo gets an empty NAVARONE.md: a repo gets one the day it has its own taste rule.

## Why
The validator is the taste judge and Navarone holds no LLM, so the taste must reach the agents in the user's own words, or they judge by their own. One central file and one local extension follow the pattern BUILDER.md and VALIDATOR.md already use, and keep the instruction to run Navarone in one place instead of copied into every repo, where the copies would drift.

## Edges
- part-of -> process
- uses -> personal:taste-is-the-human-aspect
