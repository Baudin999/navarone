# Navarone.Classifiers

The deterministic classifiers behind steps two and four of SPEC.md's flow. Each implements a contract from `Navarone.Engine/Classification/`.

- `Deterministic/DeterministicProblemClassifier`: rules over the facts. It calls a `cyclomatic` or `cognitive` problem on a method whose facts say it is a flat dispatch a false positive, a `control-coupling` problem on a method whose facts say it is a mapping a false positive, and an `lcom4` problem on a value class a false positive: every method other than a constructor is an accessor without control flow and without a call, that reads or writes at most one of the class's own fields. No rule calls any other problem, a `split-complexity` problem included, a false positive.
- `Deterministic/DeterministicReasonClassifier`: rules over the reason's text and the ids at its code. It rejects a reason whose problem's code does not carry the id, a reason of fewer than 8 words, a reason that repeats a resolution in `reasons.json` word for word, and a reason for a `split-complexity` problem that does not name every helper that adds a decision. A helper is named by its method name as a whole word, in its own case, so `Accepted`, `` `Accepted` `` and `Accepted()` all name it; overloads share a name, and a helper that adds no decision need not be named. The helpers and their decisions come on the finding from the split-complexity check, `Finding.Helpers`, so the classifier counts nothing itself. No rule rejects any other reason.

This project references the engine and nothing else. A classifier never sees a language.
