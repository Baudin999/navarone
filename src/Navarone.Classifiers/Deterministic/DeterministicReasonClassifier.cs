using System.Text.RegularExpressions;
using Navarone.Engine.Classification;
using Navarone.Engine.Findings;
using Navarone.Engine.Reasons;

namespace Navarone.Classifiers.Deterministic;

/// <summary>
/// Classifies a reason by rules over its text and the ids at its code. It rejects a reason when the problem has a
/// finding and its code does not carry the problem's id, when the reason has fewer than WordFloor words, when it
/// repeats a resolution in reasons.json word for word, or when it answers a split without naming every helper that
/// adds a decision. No rule rejects any other reason.
/// </summary>
public sealed partial class DeterministicReasonClassifier : IReasonClassifier
{
    /// <summary>The fewest words a reason needs. A reason of fewer is rejected.</summary>
    public const int WordFloor = 8;

    /// <inheritdoc />
    public string? Rejection(ReasonCase reason)
    {
        var id = reason.Problem.Id.Value;
        var words = Words(reason.Reason);

        // A fixed problem has no finding to turn green, and an id written at its code would change the code the
        // reason defends.
        var needsId = reason.Finding is not null;
        if (needsId && !reason.IdsAtCode.Contains(id))
        {
            return $"the code of {reason.Problem.Symbol.Value} carries no `// navarone: {id}` comment; put it at the code first";
        }

        if (words.Count < WordFloor)
        {
            return $"the reason has {words.Count} words; give at least {WordFloor}";
        }

        var resolutions = reason.Recorded.Select(problem => problem.Resolution).OfType<Resolution>();
        if (resolutions.Any(earlier => Words(earlier.Reason).SequenceEqual(words, StringComparer.OrdinalIgnoreCase)))
        {
            return "the reason repeats a resolution in reasons.json word for word; one reason answers one problem";
        }

        var helpers = reason.Finding?.Helpers ?? [];
        if (Unnamed(helpers, words) is { Count: > 0 } unnamed)
        {
            return $"the reason does not answer for the helpers {string.Join(", ", unnamed)}; a reason for a split names every helper that adds a decision";
        }

        return null;
    }

    // The method names of a split's helpers that add a decision and that the reason does not name as a whole word,
    // case-sensitive. Overloads share a name, so one mention covers them.
    private static List<string> Unnamed(IReadOnlyList<Helper> helpers, List<string> words) =>
        [.. helpers.Where(helper => helper.Decisions > 0).Select(helper => helper.Name).Distinct(StringComparer.Ordinal).Where(name => !words.Contains(name, StringComparer.Ordinal))];

    // The words of a text, as written: runs of letters, digits and underscores. Case, spacing and punctuation do not count.
    private static List<string> Words(string text) => [.. Word().Matches(text).Select(match => match.Value)];

    [GeneratedRegex(@"\w+")]
    private static partial Regex Word();
}
