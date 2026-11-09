# Navarone.Languages.CSharp

The C# language provider. It reads C# through Roslyn and nothing else: no tree-sitter, not as a fallback, not behind a flag.

- `CSharpProvider.cs`: implements `ILanguageProvider`. It can load a `.sln` or `.slnx` file. Loads a solution, then extracts its facts, reading nothing of what the ignores it is built with match. The facts are incomplete when MSBuild reported a failure, or when a project it reads misses a type, a namespace or an assembly it references, as an unrestored project does: compiler errors CS0006, CS0012, CS0234 and CS0246. An unrestored load reports no MSBuild failure, so only the compiler tells.
- `Loading/`: where Roslyn loads a solution.
- `Facts/`: where Roslyn's semantic model becomes facts.

This is the only project that references Roslyn. No Roslyn type leaves it: the engine receives facts, never a syntax tree or a symbol.
