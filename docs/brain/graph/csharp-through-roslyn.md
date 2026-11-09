---
kind: information
subjects: [architecture, csharp]
named-by: user
born: 2026-10-28
session: 50a7b169-0316-4b4c-8f22-a3350ebb76ba
---
# The C# provider reads C# through Roslyn, and nothing else

The C# language provider uses Roslyn. There is no tree-sitter for C#: not as a fallback, not behind a flag, not as a library default. Other languages get their own provider with their own parser.

## Why
Earlier, tree-sitter was kept behind a flag next to Roslyn instead of removed. That diluted the whole project, and a later cleanup fell back to it, so all the C# rules ended up built on the parser the user had decided against.

## Edges
- part-of -> engine-providers-metrics
