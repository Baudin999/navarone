namespace Navarone.Engine.Events;

/// <summary>
/// The builder gave a reason for a problem: it defends the code as it is. A reason for a problem with a finding now is
/// stored as its resolution; a reason for a fixed problem argues for the fix the validator refused. Defended is the
/// code the reason defends, or null in a line logged before Navarone logged it.
/// </summary>
public sealed record ResolvedEvent(string Id, string Reason, DefendedCode? Defended) : INavaroneEvent
{
    /// <summary>A reason keeps the code.</summary>
    public Position Builder { get; init; } = Position.Keep;
}
