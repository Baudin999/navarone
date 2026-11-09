# Navarone.Metrics

One folder per check in SPEC.md. Each holds a README and one `ISignal` class.

| Folder             | Check id         | Subject | Counts                                                   | Default |
|--------------------|------------------|---------|----------------------------------------------------------|---------|
| `Cyclomatic/`      | cyclomatic       | method  | McCabe's decisions plus one                              | 10      |
| `Cognitive/`       | cognitive        | method  | Campbell's breaks in the straight line, plus nesting     | 15      |
| `Lcom4/`           | lcom4            | class   | the pieces a class's method graph falls into             | 1       |
| `ControlCoupling/` | control-coupling | method  | parameters that steer which branch runs                  | 0       |
| `SplitComplexity/` | split-complexity | method  | the method's cyclomatic with its helpers, called only from it, inlined | 10 |

Each check receives its limit from `navarone.json` through `CheckLimits` and raises a finding only above it.

This project references the engine and nothing else. A check reads facts and never sees a language, a syntax tree or Roslyn. One check reads the same facts for every language.
