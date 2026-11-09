---
kind: information
subjects: [resolution, process, signal]
named-by: user
born: 2026-10-28
session: 50a7b169-0316-4b4c-8f22-a3350ebb76ba
---
# Navarone is one command that grants an id to every new problem, and the id is what a resolution hangs on

Navarone has one command, `navarone`. When it finds a new problem it grants it an id and records it in reasons.json. The agent puts that id in a comment at the code, `// navarone: 0001234`, and gives its reason with `navarone <id> <reason>`. Navarone stores the reason under that id as the problem's resolution, with the code it answered, so the signal stays silent while that code is unchanged. Navarone does not judge the reason: the validator reviews it, and a clash goes to the user. The comment holds only the id, never the prose.

## Why
Agents are running wild, building things, and Navarone is there to enforce good coding habits. The simpler it is and the fewer commands it has, the better. Comments can be gamed, so the prose lives in reasons.json where only Navarone writes, and the code holds only a reference that has to be looked up.

## Edges
- part-of -> process
