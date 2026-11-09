namespace Navarone.Engine.Facts;

/// <summary>Where a fact sits in the analysed code. Lines and columns start at 1.</summary>
public sealed record SourceLocation(string Path, int Line, int Column);
