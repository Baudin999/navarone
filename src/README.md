# src

The parts of Navarone, one project each.

- `Navarone.Engine`: runs the checks, grants ids to new problems, classifies problems and reasons, owns `reasons.json` and reports. Knows no language.
- `Navarone.Metrics`: one folder per check. Each computes over facts.
- `Navarone.Classifiers`: the deterministic classifiers for problems and reasons.
- `Navarone.Languages.CSharp`: the C# provider. Reads C# through Roslyn.
- `Navarone.Cli`: the one `navarone` command. Wires a provider, the checks and the classifiers into the engine.

Tests do not live here. They live in `tests/`.

A new language gets its own `Navarone.Languages.<Name>` project next to the C# one. The engine, the checks and the classifiers do not change.
