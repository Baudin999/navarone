using Navarone.Engine.Reasons;

namespace Navarone.Engine.Events;

/// <summary>
/// A problem a run showed: its id, check and symbol, whether it was red or green, and for a red one the hash of its
/// subject's code in the code store, so a later fix can show the code before it.
/// </summary>
public sealed record ShownProblem(string Id, string Check, string Symbol, FindingState State, string? Code);
