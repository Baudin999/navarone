---
kind: information
subjects: [refactoring, discipline, slop, process, agents]
named-by: user
born: 2026-11-04
session: d5efc488-ca42-4ecc-8753-b09da3bb927e
---
# Do not fear the refactor: every change leaves the code better, a wrong pattern gets fixed, and a big change goes to the user

Every change leaves the code better than it found it. That is why a changed line opens every signal of the method, class or module it sits in: a builder that touches a method answers all of that method's signals, the old ones included, not only the ones its own lines raised.

When a builder notices that a pattern is wrong and is leading to bad code smells, it fixes the pattern, not only the leaf where the smell shows. A crammed one-liner is often the place where a missing shape shows, as `DreamLog.Since` showed that one record held two kinds of dream. Patching the leaf leaves the cause in place, and the next change patches it again.

When the fix is a big change, the builder does not make it on its own and does not skip it either: it brings it to the user, through the open agent, with what is wrong, where it shows and what the fix would be. The user decides.

Agents avoid a big rewrite by habit, and that habit is why code bases fill with duplication and smells. Doing it correctly or not at all beats leaving rot in place.

## Why
In the user's words: "we always leave the code better than we found it. This means: we do not fear the refactor. If we notice that a pattern is incorrect and is leading to bad code smells. We fix it! If it's a big change, we bring it to the user!" A builder that only answers the signals its own lines raised leaves the code it touched no better, and slop is exactly change upon change that each leaves the foundation a little worse.

## Edges
- part-of -> thesis
- relates-to -> personal:rewrite-over-rot
- answers -> personal:rewrite-avoidance-is-claudes-pitfall
- uses -> touching-a-method-retriggers
