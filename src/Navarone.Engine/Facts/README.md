# Facts

What a provider hands the engine: the types, their fields and methods, and each method's parameters, control flow, calls and field uses.

Facts are resolved. A call or a field use names the `SymbolId` of the declaration it binds to, and the same declaration always has the same `SymbolId`. No metric has to guess what a name refers to, and two methods that share a field share it for certain.

Each method also says, as its `Dispatch`, whether it is a flat dispatch: one switch or else-if chain whose every branch is a single return or a single expression and whose every condition is one test. A flat dispatch is a mapping when every branch only returns a value or throws and it calls, assigns, increments or awaits nothing outside a throw. The provider decides both in its own language, so a classifier never sees one.

Each method's `CallSites` are its calls in the order written, each with the symbol it calls and the line it is on; `Calls` is the symbols alone, once each. The callers of a method are the inverse of the call sites.

Each type carries its own short `Name`, and each method and field its `Signature`, its declaration as the language writes it, without a body. Each field also carries where its name is and its declaration as written, so the lens can show it.

Each method carries a short `Name`, whether it is static, whether it is public and whether it is a property's or indexer's accessor, each field a `Name`, whether it is static, whether it is public and whether it is an auto-property, and each type the type it is nested in, its namespace, its assembly, whether it is an interface and whether it is a test class. A question and the class view name members with these, so neither parses a `SymbolId`.

Each method and type also carries its `Code`: the declaration as written, name included. The engine computes the fingerprint a resolution carries from it, so a change to the code, a rename included, opens the problem again. See `Reasons/`.

Each method and type also carries its `NavaroneIds`: the ids written in `// navarone: <id>` comments at it, as written. A type's code leaves out the navarone comments written anywhere in the class, and its ids are those written anywhere in it. The engine counts an id only at the code of its own problem, so an id copied onto other code does not count.

Each method also carries its `Tokens`: its body's tokens in the order written, each with its line, its text as written and its shape, the token as its provider normalised it. Every identifier has the shape `id` and every literal `lit`; a keyword or punctuation keeps its text. Comments and whitespace are no tokens. A duplication search compares shapes, so a renamed copy reads as its original and the search never sees the language.

Each control flow fact carries its span in the file, its head with its lines, columns and text, the construct as the language writes it, and the fact it is a part of, such as the if whose condition holds an `&&`. A check builds its evidence from them without seeing the language.

The facts of a codebase say whether they are `Complete`. A provider marks them incomplete when it could not resolve all of the code, such as a C# project whose packages are not restored: such facts depend on more than the code, so the engine never keeps them in its facts cache.

Syntax does not live here. A provider maps its language onto these records. When a metric needs a fact that is missing, the fact is added here and every provider supplies it.
