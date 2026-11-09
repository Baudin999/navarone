using Navarone.Engine.Findings;

namespace Navarone.Engine.Reasons;

/// <summary>
/// A problem at the end of SPEC.md's flow: the finding a check raised, the problem as recorded in reasons.json with its
/// id and resolution, and its state.
/// </summary>
public sealed record Verdict(Finding Finding, RecordedProblem Problem, FindingState State);
