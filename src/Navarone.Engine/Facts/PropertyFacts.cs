namespace Navarone.Engine.Facts;

/// <summary>
/// A property or indexer with a body: one whose getter, setter or init accessor holds code, or one with an expression
/// body. An auto-property is a field instead, as FieldFacts says. Name is its short name, such as `Total` or `this[]`.
/// IsPublic is true for one declared public. Location is where its name is written, and Written its whole declaration
/// as it stands in its file. Accessors are its accessors with a body, each one of the type's methods, the getter
/// first.
/// </summary>
public sealed record PropertyFacts(SymbolId Symbol, string Name, bool IsPublic, SourceLocation Location, Written Written, IReadOnlyList<AccessorFacts> Accessors);

/// <summary>
/// An accessor with a body: the method of the type it is, by its symbol, its side, `get`, `set` or `init`, and its own
/// code as it stands in its file, which for an expression-bodied property is its expression body.
/// </summary>
public sealed record AccessorFacts(SymbolId Method, string Side, Written Written);
