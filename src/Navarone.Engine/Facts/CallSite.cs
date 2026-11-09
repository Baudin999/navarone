namespace Navarone.Engine.Facts;

/// <summary>One call a method makes: the resolved symbol it calls and the line of the file the call is written on.</summary>
public sealed record CallSite(SymbolId Callee, int Line);
