namespace Navarone.Engine.Facts;

/// <summary>
/// Everything a provider hands the engine for one analysed codebase. Complete is false when the provider could not
/// resolve all of the code, such as a project whose packages are not restored: its facts may lack symbols a whole load
/// binds, and the next load may build other facts from the same code, so the engine never keeps them in its cache.
/// </summary>
public sealed record CodebaseFacts(IReadOnlyList<TypeFacts> Types, bool Complete)
{
    /// <summary>Every type and method these facts declare, by its symbol.</summary>
    public IReadOnlySet<SymbolId> Symbols() =>
        Types.Select(type => type.Symbol)
            .Concat(Types.SelectMany(type => type.Methods).Select(method => method.Symbol))
            .ToHashSet();
}
