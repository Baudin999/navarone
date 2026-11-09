using Navarone.Engine.Facts;
using Navarone.Engine.Findings;

namespace Navarone.Engine.Classification;

/// <summary>One problem to classify. Finding is what a check raised. Facts are the facts it was raised on.</summary>
public sealed record ProblemCase(Finding Finding, CodebaseFacts Facts);
