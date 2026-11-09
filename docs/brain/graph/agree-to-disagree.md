---
kind: information
subjects: [escalation, settling, review, agents, process]
named-by: user
born: 2026-11-01
session: 0c720208-e614-44c9-847b-38d2dd4b507f
---
# When the builder and the validator cannot agree on a signal, it escalates to the user, who settles it from both sides

The builder answers a signal from inside Navarone's loop: a fix, such as a split method or a new interface, or a
resolution that holds the code is right. The code before the fix is pre, the fix is post, and a fix can reach across
several files and add new types. The validator runs Navarone too and reviews the work with the user's taste.

When the two cannot agree on how to solve a signal, that is an escalation, a clash: the validator refused the builder's
answer, and the builder holds its position without changing the code. It goes to the user. He sees the code before the
fix, the fix, the builder's reasoning for it and the validator's reasoning against it, and judges. He answers in
brain's window, in a view of its own apart from node review, because going through clashes is a sitting that takes
dedication and time. His answer is final, and it becomes the resolution.

For now a clash holds up landing until the user has judged it. A toggle in Navarone's config switches that fast, so the
judgement can move to after landing later.

## Why
In the user's words: "When they cannot agree on how to solve a signal coming from navarone, THAT's an escalation!" A
clash between the builder and the validator is exactly the case where neither can be trusted alone, and a fix can only
be judged by seeing both sides of it. The user is the ultimate arbiter. He wants to get to know the system and see what
we are dealing with, so for now a clash waits for his judgement before the work lands.

## Edges
- uses -> brain:tool-disputes
- part-of -> process
