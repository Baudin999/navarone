namespace Navarone.Engine.Events;

/// <summary>
/// One `navarone` or `navarone --full-coverage` call: every problem it showed, red or green, and its cost in tokens,
/// the characters it printed divided by four.
/// </summary>
public sealed record RunEvent(IReadOnlyList<ShownProblem> Shown, int Tokens) : INavaroneEvent
{
    /// <summary>A run is about no one problem.</summary>
    public string? Id => null;
}
