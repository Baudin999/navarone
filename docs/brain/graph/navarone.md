---
kind: information
subjects: [thesis, slop, process, signal]
named-by: user
born: 2026-10-28
session: 50a7b169-0316-4b4c-8f22-a3350ebb76ba
---
# Navarone is a tool with no LLM, baked into the process: static analyzers, exclusions, the user's taste, resolutions and a window to settle problems

Navarone holds no judgement of its own. It contains:

- Static analyzers: cyclomatic and cognitive complexity, LCOM4 cohesion, control coupling, split complexity and duplication. Each finding is a signal that sends an agent to look.
- Ways to exclude projects and files from checking.
- The user's taste, defined clearly, so the agents get it injected into their work, above all on review.
- Resolutions: a piece of code linked to what was decided about it, so signals do not pile up and clog the context. When something has been decided, it has been decided, until the code changes.
- A lens: a static view into the code that gives an agent or the user the context a method needs.
- A window in brain where the user settles the problems that reach him.

The builder and the validator both run it. It analyses C# for now, and the spec lives in the repo.

## Why
In the user's words: "Navarone should have 0% LLM. Navarone is a tool used and baked into the process." A signal is never the solution, so the judgement belongs to the agents and to the user, and a tool that only gives signals never pretends to be more. Before, Navarone carried its own LLM judge; the validator is the taste judge now.

## Edges
- answers -> personal:agent-slop-in-large-codebases
- part-of -> thesis
