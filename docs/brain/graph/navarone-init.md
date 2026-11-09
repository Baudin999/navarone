---
kind: information
subjects: [exclusion, process, agents]
named-by: user
born: 2026-11-04
---
# navarone init writes a repo's navarone.json with its test projects ignored, and a repo with a navarone.json runs the process

## Why
A repo opts into Navarone by having a navarone.json, because Navarone only reads C# and a builder in a repo without it has nothing to run. Prefilling the test ignores means a repo starts with signals only on the code we want beautiful.

## Edges
- part-of -> process
- uses -> navarone-ignores
