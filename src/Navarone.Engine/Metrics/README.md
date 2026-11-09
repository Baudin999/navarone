# Metrics

`ISignal`: the contract every check implements. It names its id and its subject, takes the facts of a codebase and the limits from `navarone.json`, and returns a finding only where the number is above its limit.

The checks themselves do not live here. They live in `Navarone.Metrics`, so the engine runs them without knowing any of them.
