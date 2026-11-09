---
kind: information
subjects: [complexity, signal]
named-by: claude
born: 2026-10-29
---
# A split-complexity helper is any method of the root's own type or a type nested in it, and the split is measured as if inlined

A helper is a method of the root's type, or of a type nested in it, whatever its accessibility, whose callers are all the root or other helpers. A call into a separate top-level type is a hand-off, not a split. An override or interface implementation is not a helper when its contract is declared outside the root's type and the types nested in it. The split's value is the cyclomatic the root would have with its helpers inlined: its decisions plus every helper's decisions, plus one. No helper is exempt, a flat dispatch or a mapping included.

Whether a split is real is a judgement; no rule calls it real. What is deterministic is what a reason must cover: the finding lists every helper that adds a decision, and a reason that does not name each of them is rejected before it is stored.

## Why
A split is a method cut into pieces that each pass while the whole does not, so it is measured as the whole. Reading the spec literally made whole pipelines one split, such as Main at 222, so a separate top-level type stays out. Two audits showed that limiting helpers to private methods let an agent escape by making a helper internal or moving it into a nested class, and that counting one per helper rewarded inlining the split back. Exempting mapping helpers let `? true : false` hide any number of decisions, and excluding every contract method let a private interface hide helpers, so neither exemption stays.

Calling every split real by rule was weighed twice and rejected. McCabe's sum counts a collection of routines, but his limit of 10 is per module, and he told programmers to modularize subfunctions; a limit on the sum caps how much one job may hold. Of Navarone's own 12 splits about 3 were real, and in 6 of the rest only a worse fix would pass: scattering helpers into other types, or swapping NodeParser's exhaustive switch for a dictionary. No tool in the field inlines helpers; a good split and the trick have the same shape. The coverage rule exists because gemma4 accepted a reason for 0000020 that justified Prompt and said nothing of Verdict and Accepted, one decision cut in two.

## Edges
- part-of -> split-complexity
