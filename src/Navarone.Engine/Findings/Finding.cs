using Navarone.Engine.Facts;

namespace Navarone.Engine.Findings;

/// <summary>
/// A question a check asks about one subject. A check raises it only when Value is above Limit.
/// Check is the check id. Subject is the resolved symbol the CLI prints. Question is the prose the agent
/// answers, by fixing the code or through `navarone &lt;id&gt; "&lt;reason&gt;"` with the id Navarone grants the problem.
/// Helpers are the helpers of a split method, each with the decisions it adds, and null for any other finding. The
/// problem's fingerprint covers their code, so a change to any of them voids the reason.
/// Evidence is how the check came to Value, for a check that builds it, and null for any other.
/// </summary>
public sealed record Finding(
    string Check,
    SymbolId Subject,
    SubjectKind SubjectKind,
    SourceLocation Location,
    int Value,
    int Limit,
    string Question,
    IReadOnlyList<Helper>? Helpers = null,
    Evidence? Evidence = null);
