# ControlCoupling

Subject: method. Default limit: 0. A finding is raised only above the limit, so one flag parameter is red.

Control coupling, the third rung of the coupling ladder Hitz and Montazeri list: a parameter decides what the called method does, so the caller has to know how it works inside.

The value is the number of parameters with `ParameterFacts.Steers`. The provider decides which parameters steer, so this check never sees a language. The rule, as the C# provider applies it:

- A branch condition is that of an if, a conditional expression, a loop, a switch statement, a switch expression or the `when` clause of a case or an arm.
- A `bool` or `enum` parameter steers when a condition reads it directly: bare, negated, in an `&&` or `||`, or compared.
- Any other parameter steers when a condition compares it to a non-null constant or tests it with a constant or relational pattern, or when a switch on it has such labels.
- A guard, an if with no else whose body only throws or returns, counts an equality with a constant, `if (kind == "a") return 1; return 2;`, but not a range check, `if (count < 0) return;`, nor an empty-input check against zero or the empty string, `if (count == 0) return;`. A comparison with null, or an `is null` or `is not null` test, never counts, even on a nullable `bool` or `enum`, and `ArgumentNullException.ThrowIfNull(x)` is no condition.
- A parameter handed to another method, or read through a member, does not steer. A property, positional or list pattern reads through a member, so `literal is Record { Name.Count: > 1 }` does not steer, and neither does a switch whose arms test only such patterns.
- A local declared from a parameter alone and never written again reads as that parameter: after `var factor = bold;`, a condition on `factor` steers by `bold`. A chain of such copies is followed. A local written again, by an assignment, an increment, a deconstruction or a `ref` or `out` argument, or declared from anything more than the parameter, is not followed.

The question names the steering parameters. The refactor it points at turns the flag into a type.

A method whose body is a mapping, a flat dispatch whose every branch only returns a value, still counts here. The deterministic problem classifier calls that problem a false positive: a mapping is a function of its input, not a caller choosing behaviour.
