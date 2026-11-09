# Metrics

`ISignal`: the contract every check implements. It names its id and its subject, takes the facts of a codebase and the limits from `navarone.json`, and returns a finding only where the number is above its limit.

`IClassPieces`: a check that measures a class by the pieces it falls into, such as LCOM4, also hands out those pieces. The class view colours each member by its piece from it, so the window shows the very pieces the check counts and no second LCOM is computed.

The checks themselves do not live here. They live in `Navarone.Metrics`, so the engine runs them without knowing any of them.
