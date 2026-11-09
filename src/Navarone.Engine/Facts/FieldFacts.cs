namespace Navarone.Engine.Facts;

/// <summary>
/// A field of a type, or the property an auto-property stands for. Name is the short name a question shows. IsStatic is
/// true for a field that has no instance, IsPublic for one declared public, and IsProperty for an auto-property.
/// Signature is its declaration as the language writes it, such as `private readonly int count`. Location is where its
/// name is written, and Written its declaration as it stands in its file.
/// </summary>
public sealed record FieldFacts(SymbolId Symbol, string Name, bool IsStatic, bool IsPublic, bool IsProperty, string Signature, SourceLocation Location, Written Written);
