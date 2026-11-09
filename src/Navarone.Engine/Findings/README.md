# Findings

- `Finding`: a question a check asks about one subject. It carries the check id, the subject's resolved symbol, what kind of subject it is, where it is, the number measured, the limit it is above and the question in prose, and for a split method its helpers.
- `Helper`: one helper of a split method, with the method name a reason names it by and the decisions it adds to the split.
- `Evidence`: how a finding's value comes to life, for a check that builds it, cyclomatic for now: the subject's code, the base and the proof tree. `Document` writes it with its finding as the evidence document that `navarone evidence <id> --json` prints.
- `ProofItem`: one construct the check counted or chose not to count: its span, its kind, what it adds, why, and its parts. A part the rule does not count shows with adds 0. Cognitive will carry its nesting weight in adds, and split-complexity one evidence per helper summed into one value.
- `SubjectKind`: whether the subject is a method, a class or a module.
- `CodePiece`: a method's code where it stands, its file, the lines it spans and its text as written, which evidence shows.

A check raises a finding only when the number is above its limit. A finding is not a verdict. How it is classified and whether a resolution answers it lives in `Classification/`, `Reasons/` and `Verdict`.
