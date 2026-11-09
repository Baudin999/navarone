namespace Navarone.Engine.Duplication;

/// <summary>
/// The floor under which a match is not reported: its similarity, the share of the longer piece's tokens the other
/// piece matches, and its size, the longer piece's normalised tokens. The floor only drops matches that are not
/// duplication; when in doubt a pair is reported.
/// </summary>
public sealed record CloneFloor(double Similarity, int Tokens)
{
    /// <summary>The floor of the research run: similarity 0.7 and 30 tokens, low enough to show too much rather than too little.</summary>
    public static CloneFloor Research { get; } = new(0.7, 30);
}
