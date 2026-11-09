# Clashes

A clash: the validator refused the builder's answer to a signal, and the builder held its position with a new reason for the code refused. The user judges it in brain's window. SPEC.md, "Clashes", holds the rules.

- `WorktreeClashes.cs`: the clashes on one worktree during one `navarone` run. It collects the user's judgements under the question id for the subject's code as it is now, gives each keep the builder's reason as the resolution the check stores, and says which problems a clash holds red: one not judged yet, and one answered replace while its code is the code he answered about.
- `BrainAnswers.cs`: reads the user's answers where brain's window stores them, `cache/answers/navarone/<question id>.json` under brain's home. It never writes there.
- `Questions.cs`: the document `navarone disputes --json` prints, one bench of brain's generic blocks per clash not yet judged, read from the event log alone.
- `TasteRules.cs`: the numbered rules of NAVARONE.md, which a validator's replace names by number, and where Claude Code loads NAVARONE.md from.
- `Hold.cs`: a problem a clash holds red, with what `navarone` prints for it.
- `SubjectCode.cs`: a subject's code as it stands in its file, where an answer's code and a hold's place are read from.

The clash itself, `Clash` in `Events/`, is read from the log by `WorktreeHistory`, with its question id and whether it is stale, from a `Refusal` and the reason that answers it.
