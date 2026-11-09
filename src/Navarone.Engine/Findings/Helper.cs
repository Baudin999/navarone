using Navarone.Engine.Facts;

namespace Navarone.Engine.Findings;

/// <summary>
/// A helper of a split method. Name is the method name a reason names it by, the first word of its short name, so
/// overloads share one. Decisions are what the helper adds to the split: its cyclomatic minus one.
/// </summary>
public sealed record Helper(SymbolId Symbol, string Name, int Decisions);
