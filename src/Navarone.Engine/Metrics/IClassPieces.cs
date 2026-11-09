using Navarone.Engine.Facts;

namespace Navarone.Engine.Metrics;

/// <summary>
/// A check that measures a class by the pieces it falls into, such as LCOM4. The class view colours each member by its
/// piece from this, so the window shows the very pieces the check counts.
/// </summary>
public interface IClassPieces : ISignal
{
    /// <summary>The pieces of the given type, or null when the check does not measure it, such as an interface.</summary>
    IReadOnlyList<ClassPiece>? Pieces(TypeFacts type);
}

/// <summary>One piece of a class: its methods, then the instance fields they use, in declaration order.</summary>
public sealed record ClassPiece(IReadOnlyList<SymbolId> Methods, IReadOnlyList<SymbolId> Fields);
