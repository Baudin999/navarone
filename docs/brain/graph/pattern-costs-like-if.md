---
kind: information
subjects: [complexity, signal]
named-by: user
born: 2026-11-01
session: 5e740c7e-91c8-447d-ab13-ae4fdefc4440
---
# A decision costs the same in any syntax: a switch arm mixing a type test, a property pattern and a when costs its if

Cyclomatic and cognitive complexity count every test, whatever syntax writes it, as McCabe counts predicates: each type test, each subpattern that tests a value, each `when` guard, each element of a list pattern that tests a value, and each `&&`, `||`, `and`, `or` in a pattern or guard. A call, a `!` and a `not` add nothing of their own: the decision is the test of their result, and that is already counted. `s is not null` costs what `s != null` costs, and `{ A: not null }` costs its subpattern, wherever the `not` stands. This holds for `is` patterns everywhere, not only in switch arms. A bare binding such as `{ Name: var n }` tests nothing and is free, and so does an arm or case whose pattern is a discard or a `var`: only its `when` counts. A pure label list, constants or types joined with `or` such as `1 or 2 or 3`, names the cases of one branch and stays a flat dispatch; a property pattern or a `when` makes an arm not flat.

`UsesOf` in Navarone's own fact extractor shows the shape:

```csharp
return model.GetSymbolInfo(node).Symbol switch
{
    IMethodSymbol method => ...,
    IFieldSymbol { IsConst: false } field => ...,
    IPropertySymbol property when IsBackedByField(property) => ...,
    IPropertySymbol property => ...,
    IEventSymbol @event when IsBackedByField(@event) => ...,
    IParameterSymbol { ContainingSymbol: IMethodSymbol constructor } parameter
        when IsPrimary(constructor) && !InInitializer(node) => ...,
    _ => [],
};
```

The last arm is four decisions on one line:

```csharp
IParameterSymbol                                      // 1: the arm's type test
  { ContainingSymbol: IMethodSymbol constructor }     // 1: the nested type test
  parameter when                                      // 1: the when
    IsPrimary(constructor) && !InInitializer(node)    // 1: the &&; the calls and ! add 0
```

Written honestly it is

```csharp
if (symbol is IParameterSymbol parameter
    && parameter.ContainingSymbol is IMethodSymbol constructor
    && IsPrimary(constructor)
    && !InInitializer(node))
```

and it costs the same. Each arm also silently negates every arm above it: the plain `IPropertySymbol property` arm means a property not backed by a field, which only reading the arms in order tells. `InInitializer(node)` asks where the syntax sits, not what kind of symbol this is; it landed in a switch over symbol kinds because one more `when` was free. `AccessorsUsed` does the same: `PrefixUnaryExpressionSyntax unary when unary.Operand == access && unary.OperatorToken.Kind() is SyntaxKind.PlusPlusToken or SyntaxKind.MinusMinusToken` is a type test, a guard, an `&&` and an `or` in one arm.

A reason that calls such a switch "a table of the language's forms" or "just a switch" does not make it cheaper. Navarone's own switch methods were all excused that way and the reasons were accepted; that is the steering this rule stops.

## Why
An if in the matched property, a when, a method call in the when: in the user's words, "it's chaos". Three syntaxes for conditions in one arm hide how many decisions the arm makes, and the arms' order hides the negations between them. An agent reaches for that density because it counted as one branch. Navarone exists to steer an agent toward great code, so every syntax for the same decision must cost the same: then the dense form gains the agent nothing, and the code that reads clearly is the cheap one. Counting by McCabe's predicates, and not the calls inside them, keeps one decision from being counted twice.

## Edges
- part-of -> cyclomatic
