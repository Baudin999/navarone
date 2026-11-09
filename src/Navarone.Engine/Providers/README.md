# Providers

`ILanguageProvider`: the contract a language provider implements. It loads a codebase at a path and returns its facts. `CanLoad` says whether a file is a codebase it reads, such as a `.sln`; the CLI analyses every such file under the repo root, and the engine merges their facts so a subject two codebases share is one subject.

Implementations do not live here. They live in their own `Navarone.Languages.<Name>` project, so the engine never references a parser.
