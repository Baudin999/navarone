namespace Navarone.Engine.Reasons;

/// <summary>
/// The agent's reason for a problem, stored through `navarone &lt;id&gt; "&lt;reason&gt;"`. Fingerprint is the
/// fingerprint of the code the reason answered and Value the finding's value it answered: once either changes, the
/// resolution no longer counts.
/// </summary>
public sealed record Resolution(string Reason, CodeFingerprint Fingerprint, int Value);
