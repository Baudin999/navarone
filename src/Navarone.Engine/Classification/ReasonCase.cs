using Navarone.Engine.Findings;
using Navarone.Engine.Reasons;

namespace Navarone.Engine.Classification;

/// <summary>
/// One reason to classify, with what it answers. Finding is the problem's finding in the code as it is now, or null
/// for a fixed problem, whose reason argues for the fix the validator refused, and Problem its entry in reasons.json.
/// IdsAtCode are the ids written at the problem's code. Recorded is every problem in reasons.json, this one included,
/// with its resolution.
/// </summary>
public sealed record ReasonCase(
    Finding? Finding,
    RecordedProblem Problem,
    IReadOnlyList<string> IdsAtCode,
    string Reason,
    IReadOnlyList<RecordedProblem> Recorded);
