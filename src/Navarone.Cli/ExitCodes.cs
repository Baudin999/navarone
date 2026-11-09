namespace Navarone.Cli;

/// <summary>What `navarone` exits with.</summary>
internal static class ExitCodes
{
    /// <summary>`navarone`: every problem it shows is green, or it shows none.</summary>
    public const int AllGreen = 0;

    /// <summary>`navarone`: at least one problem it shows is red.</summary>
    public const int AnyRed = 1;

    /// <summary>`navarone &lt;id&gt; "&lt;reason&gt;"`: the reason is stored as the problem's resolution.</summary>
    public const int Resolved = 0;

    /// <summary>`navarone &lt;id&gt; "&lt;reason&gt;"`: a rule rejected the reason, and nothing is stored.</summary>
    public const int Rejected = 1;

    /// <summary>`navarone &lt;id&gt; keep` and `navarone &lt;id&gt; replace &lt;rule&gt; "&lt;finding&gt;"`: the validator's position is logged. `navarone &lt;id&gt; large "&lt;text&gt;"`: the large change is the problem's answer and is logged. `navarone &lt;id&gt; settled`: the user's decision on a large change is logged.</summary>
    public const int Recorded = 0;

    /// <summary>`navarone init`: navarone.json is written, or was there already and is left as it is.</summary>
    public const int Initialized = 0;

    /// <summary>`navarone disputes --json`: the clashes not yet judged are listed.</summary>
    public const int Listed = 0;

    /// <summary>`navarone evidence &lt;id&gt; --json`: the problem's evidence document is printed. `navarone lens ...`: what the lens shows is printed. `navarone duplicates`: the clone pairs are printed. `navarone events --json`: the event log is printed. `navarone code &lt;hash&gt;`: the stored code is printed.</summary>
    public const int Printed = 0;

    /// <summary>`navarone` was called with arguments of none of its forms, the repo has no codebase under its root, `navarone` finds no commit to measure the change from, a git command fails or runs past its timeout, navarone.json or reasons.json cannot be read, navarone.json raises a limit above its default, the id matches no problem, a keep or replace finds nothing to accept or refuse, a replace has no finding or names no rule of NAVARONE.md, a large has no text or names a problem whose code does not carry its id, a settled names a problem with no large change or in a clash, or takes neither keep nor replace, the event log cannot be read, no code is stored under the hash, a lens does not parse, names a kind or view not built, or names no method in the codebase, or `navarone duplicates` takes an option it cannot read.</summary>
    public const int UsageError = 2;
}
