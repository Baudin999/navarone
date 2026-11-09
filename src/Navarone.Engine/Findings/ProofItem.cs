using Navarone.Engine.Facts;

namespace Navarone.Engine.Findings;

/// <summary>
/// One construct a check counted, or chose not to count, toward a finding's value. Span is where it stands in its file,
/// Kind names it as the language writes it, such as `if`, `when` or `&amp;&amp;`, Adds is what it adds to the value
/// and Why the rule that says so. Parts are the constructs inside it, such as the `||` and `&amp;&amp;` in an if's
/// condition, each with its own span and weight. A construct the rule does not count shows with Adds 0, so the rule is
/// visible where a reader expects a count.
/// </summary>
public sealed record ProofItem(SourceSpan Span, string Kind, int Adds, string Why, IReadOnlyList<ProofItem> Parts)
{
    /// <summary>What the item adds with all its parts.</summary>
    public int Total() => Adds + Parts.Sum(part => part.Total());
}
