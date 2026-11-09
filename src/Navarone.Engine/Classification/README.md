# Classification

The contracts of the deterministic classifiers, steps two and four of SPEC.md's flow.

- `IProblemClassifier`: says why a problem, given as a `ProblemCase` of the finding and the facts it was raised on, is a false positive of its check, or nothing when no rule calls it one. A false positive is green without a reason.
- `IReasonClassifier`: says why the agent's reason, given as a `ReasonCase` of the finding, the problem, the ids at its code, the reason and every problem in `reasons.json`, is rejected, or nothing when no rule rejects it. A reason no rule rejects is stored as the problem's resolution.

Implementations do not live here. They live in `Navarone.Classifiers`.
