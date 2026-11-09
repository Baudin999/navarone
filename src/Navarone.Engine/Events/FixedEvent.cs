namespace Navarone.Engine.Events;

/// <summary>
/// A problem open on this worktree is gone from the code and its code changed: the builder changed the code because of
/// Navarone. Before is the hash of the code the problem was open on, the last red run's or the code a refusal refused,
/// After the hash of the subject's code now, or null when the subject is gone. SecondRound is true when the validator refused a fix or a
/// reason for the problem on this worktree before.
/// </summary>
public sealed record FixedEvent(string Id, string Check, string Symbol, string Before, string? After, bool SecondRound) : INavaroneEvent
{
    /// <summary>A fix replaces the code.</summary>
    public Position Builder { get; init; } = Position.Replace;
}
