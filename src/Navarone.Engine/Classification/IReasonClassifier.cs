namespace Navarone.Engine.Classification;

/// <summary>Classifies the agent's reason for a problem by rules: whether Navarone refuses to store it.</summary>
public interface IReasonClassifier
{
    /// <summary>Why the given reason is rejected, or null when no rule rejects it.</summary>
    string? Rejection(ReasonCase reason);
}
