using System.Text.Json.Serialization;
using Navarone.Engine.Facts;

namespace Navarone.Engine.Reasons;

/// <summary>
/// One problem in reasons.json, keyed by its id. Check and Symbol are what the id belongs to. Fingerprint is the
/// fingerprint of the problem's code as Navarone last saw it, and Value the finding's value then. Resolution is the
/// last reason the agent gave through `navarone &lt;id&gt; "&lt;reason&gt;"` that no rule rejected, or null while there
/// is none.
/// </summary>
public sealed record RecordedProblem(
    ProblemId Id,
    string Check,
    SymbolId Symbol,
    CodeFingerprint Fingerprint,
    int Value,
    Resolution? Resolution)
{
    /// <summary>Whether the resolution answers the problem as it is now: its code and its value.</summary>
    [JsonIgnore]
    public bool Resolved => Resolution is { } resolution && resolution.Fingerprint == Fingerprint && resolution.Value == Value;
}
