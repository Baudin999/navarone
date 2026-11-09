namespace Navarone.Engine.Events;

/// <summary>
/// A side's position on a problem: keep the code as it is, or replace it. The builder keeps when it gives a reason and
/// replaces when it fixes. The validator keeps when it accepts and replaces when it sends a finding back. The user
/// answers one of the two.
/// </summary>
public enum Position
{
    /// <summary>The code stays as it is.</summary>
    Keep,

    /// <summary>The code must change.</summary>
    Replace,
}
