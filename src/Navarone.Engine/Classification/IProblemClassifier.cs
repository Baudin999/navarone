namespace Navarone.Engine.Classification;

/// <summary>Classifies a problem by rules: whether it is a false positive of its check.</summary>
public interface IProblemClassifier
{
    /// <summary>Why the given problem is a false positive of its check, or null when no rule calls it one.</summary>
    string? FalsePositive(ProblemCase problem);
}
