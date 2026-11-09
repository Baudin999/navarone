namespace Navarone.Engine.Events;

/// <summary>
/// `navarone &lt;id&gt; large "&lt;text&gt;"`: the real fix of the problem is a large change, bigger than the task.
/// Text says what is wrong, where it shows and what the fix is, and it is the problem's answer, so the task lands; the
/// agent names it in its report to the open agent, who brings it to the user. Code is the hash of the subject's code in the
/// code store.
/// </summary>
public sealed record LargeChangeEvent(string Id, string Check, string Symbol, string Text, string Code) : INavaroneEvent;
