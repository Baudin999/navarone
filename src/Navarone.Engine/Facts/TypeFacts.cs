namespace Navarone.Engine.Facts;

/// <summary>
/// A class or struct: its own fields and its own methods. LCOM4 builds its graph from these.
/// Name is the type's own short name, without its namespace or the type it is nested in.
/// ContainingType is the type this one is declared in, or null for a top-level type.
/// Namespace is the namespace it is declared in, in full, or empty for the global namespace. Assembly is the name of
/// the assembly that compiles it.
/// Code is the type's declaration as written, name and members included. The engine computes the
/// fingerprint a reason carries from it, so any change to the class, a rename included, voids its reason.
/// Written is its code as it stands in its file, the first declaration's for a partial class.
/// IsInterface is true for an interface, whose default methods have no state of their own.
/// IsTest is true for a class that holds tests, which the provider decides in its own language.
/// NavaroneIds are the ids written in `// navarone: &lt;id&gt;` comments at the class, as written.
/// </summary>
public sealed record TypeFacts(
    SymbolId Symbol,
    string Name,
    SymbolId? ContainingType,
    string Namespace,
    string Assembly,
    SourceLocation Location,
    IReadOnlyList<FieldFacts> Fields,
    IReadOnlyList<MethodFacts> Methods,
    bool IsInterface,
    bool IsTest,
    string Code,
    Written Written,
    IReadOnlyList<string> NavaroneIds);
