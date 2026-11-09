using Navarone.Engine.Facts;

namespace Navarone.Engine.Duplication;

/// <summary>
/// Two pieces of code whose normalised tokens match: Similarity is the share of the longer piece's tokens the other
/// piece matches, in order, and Tokens the longer piece's size. SameNames is the share of the matched identifiers
/// spelled the same on both sides, 0 when no identifier matched: a copy keeps most of its names, and two pieces that
/// only share a shape keep few. It is reported beside the similarity and filters nothing.
/// </summary>
public sealed record ClonePair(ClonePiece First, ClonePiece Second, double Similarity, int Tokens, double SameNames);

/// <summary>
/// One side of a clone pair: the method it lies in, its file and its first and last line, and its lines as written.
/// The piece may be a whole method or a block inside one.
/// </summary>
public sealed record ClonePiece(SymbolId Symbol, string Path, int FirstLine, int LastLine, IReadOnlyList<CloneLine> Lines);

/// <summary>A line of a piece as written. Differs is true when it holds a token the other piece does not match.</summary>
public sealed record CloneLine(int Number, string Text, bool Differs);
