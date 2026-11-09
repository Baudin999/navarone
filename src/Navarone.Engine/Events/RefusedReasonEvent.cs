namespace Navarone.Engine.Events;

/// <summary>
/// The validator ran `navarone &lt;id&gt; replace &lt;rule&gt; "&lt;finding&gt;"` on a problem resolved with a reason:
/// the code must change, Rule is the rule of the user's taste it breaks and Finding says why. Check and Symbol are the
/// problem's. Before is the hash of the code the reason defends. Rule is null in a line logged before a refusal named
/// its rule.
/// </summary>
public sealed record RefusedReasonEvent(string Id, string Check, string Symbol, string Finding, string Before, TasteRule? Rule) : INavaroneEvent
{
    /// <summary>The builder gave a reason, so it kept the code.</summary>
    public Position Builder { get; init; } = Position.Keep;

    /// <summary>The validator sends a finding back.</summary>
    public Position Validator { get; init; } = Position.Replace;
}
