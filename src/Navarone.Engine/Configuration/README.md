# Configuration

The model of `navarone.json`, which lives at the root of the analysed repo:

    { "limits": { "cyclomatic": 8 }, "ignore": { "files": ["src/Generated/**"], "projects": ["*.Tests"] } }

- `NavaroneConfig`: the whole file: the limits and the ignores. `Init` writes the file `navarone init` writes, the test projects ignored, unless a `navarone.json` is there.
- `CheckLimits`: each check's limit, keyed by check id, with the defaults from SPEC.md's table. A check missing from the file, or the file itself missing, takes its default. A limit for a check id Navarone does not know is an error, so a typo never leaves a check at its default unnoticed. A limit above its default is an error too: the file may tighten a limit, never loosen it, because the agent runs Navarone and could otherwise raise a limit until its findings are gone.
- `Ignores`: what the repo ignores, asked as `IgnoresFile(path)` and `IgnoresProject(name)`. A file glob matches a file's whole path relative to the repo root; a project glob matches a project's name, its project file's name without the extension. `**` matches any depth, `*` any characters but `/`, `?` one character but `/`. An ignore has no ceiling: the user trusts agents to change it and reverts by hand.
- `IgnoreSection`: the `ignore` section as read, checked at the boundary. A missing `ignore` or list ignores nothing. An `ignore` with a key other than `files` and `projects`, or a glob that is null or empty, is an error.

A top-level key other than `limits` and `ignore`, such as a misspelled `ignores`, is an error too, so a typo never leaves a section unread.

| Check            | Default |
|------------------|---------|
| cyclomatic       | 10      |
| cognitive        | 15      |
| lcom4            | 1       |
| control-coupling | 0       |
| split-complexity | 10      |

A check receives the limits in `ISignal.Measure` and raises a finding only above its own limit. split-complexity also reads the cyclomatic limit.

The wiring hands the ignores to the provider, which reads no fact from ignored code. So it is gone from every check, the lens and the duplicates, and `reasons.json` drops its problems as it drops a problem whose code is gone.
