namespace Navarone.Engine.Reasons;

/// <summary>Whether a problem still asks its question.</summary>
public enum FindingState
{
    /// <summary>The problem asks its question: no resolution answers its code as it is now.</summary>
    Red,

    /// <summary>
    /// The problem is silent: a rule calls it a false positive, or a resolution answers its code as it is now and the
    /// code carries the problem's id in a comment.
    /// </summary>
    Green,
}
