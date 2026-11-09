namespace Navarone.Engine.Events;

/// <summary>
/// The user decided a problem: a clash he judged in brain's window, which the next `navarone` run collected, or a large
/// change the open agent brought him, which an agent logged with `navarone &lt;id&gt; settled`. Check and Symbol
/// are the problem's. User is his decision: keep the code as it is, or replace it. Words are what he said with it, or
/// null. Code is the hash of the subject's code he decided about, or null when the subject is gone.
/// </summary>
public sealed record SettledEvent(string Id, string Check, string Symbol, Position User, string? Words, string? Code) : INavaroneEvent;
