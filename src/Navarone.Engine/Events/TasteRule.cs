namespace Navarone.Engine.Events;

/// <summary>
/// A rule of the user's taste, as NAVARONE.md lists it under "The rules": its Number there and its Text, without the
/// number. The validator's refusal names the rule the code breaks, and a clash's question takes its title from it.
/// </summary>
public sealed record TasteRule(int Number, string Text);
