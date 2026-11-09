namespace Navarone.Engine.Events;

/// <summary>
/// The validator ran `navarone &lt;id&gt; replace &lt;rule&gt; "&lt;finding&gt;"` on a fixed problem: the code must
/// still change, Rule is the rule of the user's taste it breaks and Finding says why. Check and Symbol are the
/// problem's. Before and After are the hashes of the fix it refuses, as its fixed event holds them. Rule is null in a
/// line logged before a refusal named its rule.
/// </summary>
public sealed record RefusedFixEvent(string Id, string Check, string Symbol, string Finding, string Before, string? After, TasteRule? Rule) : INavaroneEvent
{
    /// <summary>The builder fixed, so it replaced the code.</summary>
    public Position Builder { get; init; } = Position.Replace;

    /// <summary>The validator sends a finding back.</summary>
    public Position Validator { get; init; } = Position.Replace;
}
