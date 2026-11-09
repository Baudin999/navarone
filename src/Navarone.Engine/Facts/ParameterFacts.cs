namespace Navarone.Engine.Facts;

/// <summary>
/// A parameter of a method. Steers is true when a branch condition of the method reads the parameter as a flag: the
/// provider decides, so a metric never sees the language.
/// </summary>
public sealed record ParameterFacts(SymbolId Symbol, string Name, bool Steers);
