---
kind: information
subjects: [refactoring, architecture, agents, process]
named-by: user
born: 2026-11-05
session: 23326471-a33c-4a0f-8c9f-635e400cec58
---
# A large architectural or refactoring change flows through the open agent, who may send a second builder for it

When answering a signal properly needs a bigger change than the task, the agent that sees it, usually the validator
since it looks up at the architecture, names it in its report to the open agent, and Navarone logs it. The open agent
brings it to the user. When he decides, the open agent may send a second builder for the change. The task it came from
still lands, and Navarone logs the user's decision as an event.

## Why
In the user's words: "a change is big, that needs to flow through you, the open agent with whom I talk. You can decide
to spin up a 2nd builder agent. We log events." A large change needs a conversation about its shape and timing, which a
keep or replace click in the window cannot hold.

## Edges
- part-of -> process
- refines -> refactoring-discipline
