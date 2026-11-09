# Cognitive

Subject: method. Default limit: 15. A finding is raised only above the limit.

Cognitive complexity, from Campbell and the SonarSource white paper. One for every break in the straight line of the code, and one more for each level of nesting it sits at.

Reads `MethodFacts.ControlFlow` with each fact's `NestingDepth` and `Continues`, and `Calls` for recursion.

- If, switch, loop, catch and conditional cost 1 plus their depth. Lambdas and local functions add depth and cost nothing themselves.
- Else, else if and goto cost 1 flat. The cases of a switch cost nothing.
- A `when` guard, a subpattern or list element that tests a value, and the length a list pattern tests cost 1 flat: each is a decision of its own, as it is in an if. A `not` in a pattern costs nothing, like a `!`.
- A run of the same `&&` or `||` costs 1; each change of operator starts a new run.
- A method that calls itself costs 1.
- A null check, `NullCheck`, costs nothing: Campbell exempts the null-coalescing shorthand.

A staircase of nested loops and ifs scores far above a flat switch with the same cyclomatic number.
