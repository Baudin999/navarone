namespace Navarone.Engine.Events;

/// <summary>
/// Navarone granted the id to a new problem, or a run shows red a problem that is not open on this worktree: it was
/// green or gone, or the worktree's runs never showed it.
/// </summary>
public sealed record RaisedEvent(string Id, string Check, string Symbol) : INavaroneEvent;
