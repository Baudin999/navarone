using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Findings;

namespace Navarone.Engine.Metrics;

/// <summary>A check: computes one number over facts and turns what is above its limit into questions. It never sees a language.</summary>
public interface ISignal
{
    /// <summary>The stable check id, such as "cyclomatic". A problem's id belongs to it, and navarone.json keys its limit by it.</summary>
    string Id { get; }

    /// <summary>Whether this check's findings are about a method, a class or a module.</summary>
    SubjectKind Subject { get; }

    /// <summary>Measures the facts and returns one finding per subject whose value is above this check's limit in the given limits.</summary>
    IReadOnlyList<Finding> Measure(CodebaseFacts facts, CheckLimits limits);
}
