namespace Navarone.Engine.Facts;

/// <summary>
/// A symbol the provider has resolved, named by its fully qualified, language specific display string.
/// Two facts that name the same SymbolId refer to the same declaration. A metric compares SymbolIds and never parses them.
/// </summary>
public readonly record struct SymbolId(string Value);
