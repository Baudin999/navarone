# Events

Navarone's event log and its code store, in Navarone's own data folder, `~/.local/share/navarone`. SPEC.md, "The event log", holds the rules.

- `NavaroneEventHandler.cs`: the only code that writes the log and reads it back, and that stores and reads code. It appends each batch of events to the month's file in one write under the lock on `events/.lock` and says on one line to its error writer, losing the events, when it cannot, skips a last line still being written and a torn line, lists the torn lines it skipped for `navarone events --json` to name on stderr, and stores code once under the SHA-256 of its text.
- `LogFolder.cs`: the events folder at the file level: the append under the lock, which starts a line of its own after a torn one, the month files and their complete lines. `LogLine.cs`: one such line, the event it holds or its file and line when it is torn.
- `INavaroneEvent.cs`: the contract every event type implements, with the type name each is logged under.
- `RunEvent`, `CostEvent`, `RaisedEvent`, `FixedEvent`, `ResolvedEvent`, `RefusedFixEvent`, `RefusedReasonEvent`, `AcceptedEvent`, `LargeChangeEvent`, `SettledEvent`: one type per event. `ShownProblem` is a problem a run showed, `DefendedCode` the code a reason defends and where it is, and `TasteRule` the rule of NAVARONE.md a refusal names.
- `Refusal.cs`: the validator's refusal of a fix or a reason that names its rule, as a clash shows it, with what is wrong: its finding's first sentence.
- `Clash.cs`: a clash the user has not judged yet, the refusal and the reason that holds against it, with the subject's code as the last run showed it, whether it is stale and its question id.
- `Position.cs`: keep or replace, each side's position on a problem.
- `EventPlace.cs`: where an event happened: the repo's main checkout, the worktree, its branch and HEAD.
- `LoggedEvent.cs`: an event with its time and place, as one flat line of JSON, and the document `navarone events --json` prints.
- `WorktreeHistory.cs`: what the log says of each problem on one worktree: whether it is open and on which code, its last fix, whether the validator refused an answer for it, the validator's standing refusal of it, each clash not yet judged and each one the user answered replace to.
