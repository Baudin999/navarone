---
kind: idea
subjects: [lens, review, agents]
named-by: user
born: 2026-11-03
session: 93699b55-d495-4cf9-91af-42614cce3fe0
---
# A lens into the code: Navarone gives an agent or the user, statically, the context a method needs to be judged

A diff is not always enough; sometimes the reader needs more code. Since Navarone has the source tree, it finds that context statically instead of letting an agent search for it with tool calls. The builder uses it when a signal sends it to look, the validator when it reviews with the user's taste, and the user in brain's window when a clash reaches him. What the lens shows follows how the user reads:

- a private method: the method, with the declarations of the fields it uses
- a public method: its class and its call sites, because a public method is the class's API
- a refactor: the code before and everything it became
- a call the code depends on: the callee's body, one level down

Navarone already resolves members, callers and helpers for LCOM4, coupling and the split trick, so the lens is built from what the analyzers know.

In the user's words: "I hope that, since we have the source tree, we can find these things statically and build a 'lens into the code'." And: "navarone's static tool to look into code is a beautiful and powerful feature."

## Why
A method does not run in a vacuum, so judging it needs the code around it. Finding that context with tool calls is slow and uncertain, while the source tree already holds it, and Navarone has resolved most of it for the analyzers.

## Edges
- about -> personal:methods-do-not-run-in-a-vacuum
- part-of -> process
