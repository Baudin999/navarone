---
kind: information
subjects: [lens]
named-by: user
born: 2026-11-03
session: 93699b55-d495-4cf9-91af-42614cce3fe0
---
# The lens is a small language: a subject, then views of it, each with data after a colon and its own views in brackets

```
navarone lens method Burns.Apply callers:3 [expand:1 class:short] expand:1 class:short
navarone lens method MethodLens.Show:2 expand:1
navarone lens class Burns
navarone lens module Navarone.Engine
```

The subject is a kind, `method`, `class` or `module`, and a name. A name that matches several overloads takes a number after a colon, 1-based in file and line order, because bash takes the parentheses of `Show()`; without a number every overload is shown, each heading saying `overload N of M`. Every view after the subject is an operation on that subject, not a chain: `expand:1` expands the subject's calls whatever came before it. A view's data follows a colon, several values separated by commas, such as `callers:3,lines:5`; a view without data takes its default. A view's own views follow it in square brackets and apply to each of its results, to any depth: `callers:3 [expand:1 class:short]` shows three call sites, each expanded with its class. A module is a csproj. No code is shown twice in one output: a method or outline shown before is a line pointing back to it.

```
lens  = kind name (":" word)? view*
view  = step ("[" view* "]")?
step  = word (":" arg ("," arg)*)?
```

It has its own lexer, parser and execution. Navarone joins its arguments and lexes them as one text, so nothing is quoted. A bracket pair with no space inside, such as `[expand:1]`, is a bash glob pattern; the parser accepts it, and the spec says so.

## Why
The context a reader needs depends on the question: sometimes the class, sometimes the chain of calls to a depth, sometimes how callers call the method with some surrounding code, and for coupling the modules and how dependencies flow. In the user's words: "I think that pipes in a lens call are correct. I am afraid that 'flags' get very very unwieldy", and on nesting: "if we are designing a DSL, we might as well do it right. I think a small 'resursive' step is always a good idea." A pipe was dropped because it promises that each stage transforms the last, while every view works on the one subject; quoting was dropped at the user's wish, and round and curly brackets because bash takes them.

## Edges
- part-of -> lens-into-the-code
