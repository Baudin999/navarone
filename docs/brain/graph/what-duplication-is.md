---
kind: information
subjects: [duplication, slop, signal]
named-by: user
born: 2026-11-03
session: 769f10d1-9c21-4593-8d27-935625d900ea
---
# Duplication is clearly copied code blocks, clearly repeating blocks, and clearly the same idea implemented multiple times

The duplications Navarone looks for are three:

1. Clearly copied code blocks: a whole method, or a block copied into another method, renamed or tweaked.
2. Clearly repeating blocks, like the weight ladder in the Stop the slop talk that appears twice, once per country: the same logic repeated with only the data changing, branches doing a table's job.
3. Clearly the same idea implemented multiple times, like formatDate, formatDateV2, formatDateNew and toDateString in the Xebia talk: four functions, one job, which need not look alike.

Idiomatic code is never duplication. A return statement, a case statement, a single method call like FirstOrDefault, or a lambda parameter's name is the alphabet of the language and carries no knowledge. What makes code a duplicate is the same piece of knowledge written twice, so that a change to one must be made to the other.

## Why
A detector that matches on how code looks reports return null against return null and drowns the real copies, and that makes us doubt it understands what duplicate code is. The duplication that hurts is what agents leave in a large code base: code they copied because they did not find or trust what was there.

## Edges
- relates-to -> personal:duplication-is-a-clear-signal
- relates-to -> personal:agent-slop-in-large-codebases
- part-of -> duplication
