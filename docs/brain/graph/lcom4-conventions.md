---
kind: information
subjects: [cohesion, signal]
named-by: claude
born: 2026-10-29
---
# lcom4 skips test classes and methods that touch nothing, and counts statics as standard LCOM4 does

## Why
About 60 of zdragon-swarm's 96 lcom4 findings were test classes whose independent tests are the point; stubs say nothing about cohesion; and a shared static logger must not make unrelated methods look cohesive.

## Edges
- part-of -> lcom4
