---
kind: information
subjects: [resolution, signal]
named-by: user
born: 2026-10-28
session: 50a7b169-0316-4b4c-8f22-a3350ebb76ba
---
# Touching a method retriggers its reasons

A reason in reasons.json belongs to one finding, named by the resolved symbol the CLI prints with the question. When the method's code changes, its reasons no longer count and the finding is red again until a new reason passes. A rename is a change. A reason whose code is gone is removed by Navarone.

## Why
If we touch a method, that's the signal to reevaluate. A reason answered the code as it was, not as it is now.

## Edges
- part-of -> process
