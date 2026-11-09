# Reasons

The model of `reasons.json`, which lives at the root of the analysed repo. Only Navarone writes it. An agent never edits it; it answers a problem through `navarone <id> "<reason>"`, and Navarone stores a reason no rule rejects as the problem's resolution.

- `ReasonsFile`: the whole file. It holds every problem by id and `granted`, the last id Navarone granted. When it grows too big it moves to sqlite.
- `ProblemId`: the id Navarone grants to a new problem: a seven digit counter, `0000001` first. It is granted in order and never twice, not even after its problem is removed. The agent puts it in a comment at the code, `// navarone: <id>`. The comment holds only the id, never the prose.
- `RecordedProblem`: one problem. Its id belongs to its check and its resolved symbol. It holds the fingerprint of its code and the finding's value as Navarone last saw them, and its resolution. `Resolved` says whether the resolution answers the problem as it is now: its fingerprint and its value both the problem's.
- `Resolution`: the agent's last reason that no rule rejected, with the fingerprint of the code and the value of the finding it answered.
- `CodeFingerprint`: the SHA-256 of the subject's symbol and the `Code` its facts carry. The symbol is part of it, so a rename opens a problem again even where the code's own text did not change, such as a method whose class was renamed.
- `FindingState`: green for a false positive a rule calls, without a reason. Any other problem is red until its resolution answers its code as it is now and the code carries the problem's id.
- `Verdict`: a finding with its recorded problem and its state.

The rules, from SPEC.md:

- Navarone grants an id to each new problem: a finding whose check and symbol match no problem in the file.
- An id matches only its own check and symbol. An id copied onto other code does not match and does not count.
- One reason answers one problem. A new reason takes the place of the resolution before it.
- Changing the subject's code changes its fingerprint and opens the problem again, and so does a change to the finding's value, such as a check that counts differently. The problem keeps its id and is red until a reason for the new code is stored.
- The `// navarone: <id>` comment carries the id over a rename. When a problem's recorded symbol is gone and the comment with its id now sits at another method or class with a finding of the same check, that is the same problem renamed. It keeps its id, takes the new symbol and fingerprint, and is red again.
- When a problem's recorded symbol still exists, its id at other code is a copy and does not count.
- Navarone removes a problem whose code is gone, on every `navarone` run, after it has carried renamed problems over.

A number alone makes an agent game the metric. A question it must answer in prose makes it account for its code.
