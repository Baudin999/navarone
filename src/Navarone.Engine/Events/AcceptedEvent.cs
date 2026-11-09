namespace Navarone.Engine.Events;

/// <summary>
/// The validator ran `navarone &lt;id&gt; keep`: it accepts the builder's answer. Builder is that answer: replace for a
/// fix, keep for a reason.
/// </summary>
public sealed record AcceptedEvent(string Id, Position Builder) : INavaroneEvent
{
    /// <summary>The validator accepts the code as it is.</summary>
    public Position Validator { get; init; } = Position.Keep;
}
