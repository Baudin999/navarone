namespace Navarone.Engine.Lenses;

/// <summary>A word of a lens as written, and the column of the lens it starts at, from 1, so an error can say where.</summary>
public readonly record struct LensWord(string Text, int Column);
