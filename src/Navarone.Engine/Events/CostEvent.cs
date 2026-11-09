namespace Navarone.Engine.Events;

/// <summary>
/// The cost of one agent-facing command that checks nothing, such as `navarone lens`: its name and its tokens, the
/// characters it printed divided by four. It is no run: a run is what "previous run" and a fix are read from.
/// </summary>
public sealed record CostEvent(string Command, int Tokens) : INavaroneEvent
{
    /// <summary>A cost is about no one problem.</summary>
    public string? Id => null;
}
