---
kind: information
subjects: [coupling, signal]
named-by: claude
born: 2026-10-29
---
# A parameter steers a branch when a condition reads it as a flag: a bool, an enum or a literal comparison, never a validity guard

## Why
At limit 0, counting every null guard would turn almost every method red; the flag argument is the slop pattern worth catching.

## Edges
- part-of -> control-coupling
