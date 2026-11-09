using Navarone.Engine.Events;
using Navarone.Engine.Facts;

namespace Navarone.Engine.Clashes;

/// <summary>
/// The clashes on one worktree during one `navarone` run, over the code as it is now: it collects the user's judgements
/// from brain's answers, and says which problems a clash holds red.
/// </summary>
internal sealed class WorktreeClashes(WorktreeHistory history, IReadOnlyDictionary<SymbolId, SubjectCode> subjects)
{
    /// <summary>
    /// Collects the user's judgement of each clash from brain's answers, settles it in the history, and returns each
    /// clash judged with the settled it logs. The answer is looked up under the question id for the subject's code as it
    /// is now, so an answer about code that changed since is never found, and the question is listed again as stale.
    /// </summary>
    public List<(Clash Clash, SettledEvent Answer)> Collect(BrainAnswers answers)
    {
        var judged = new List<(Clash Clash, SettledEvent Answer)>();
        foreach (var clash in history.Clashes())
        {
            var refusal = clash.Refusal;
            var code = CodeNow(refusal.Symbol);
            var asked = clash with { CodeNow = code };
            if (answers.Of(asked.QuestionId()) is not { } answer)
            {
                continue;
            }

            var answered = new SettledEvent(refusal.Id, refusal.Check, refusal.Symbol, answer.User, answer.Words, code);
            judged.Add((clash, answered));
            history.Settle(answered);
        }

        return judged;
    }

    /// <summary>
    /// The resolution of each problem whose clash the user answered keep to, by its id: the builder's reason, which the
    /// user holds right.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Kept(IReadOnlyList<(Clash Clash, SettledEvent Answer)> judged)
    {
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (clash, answer) in judged)
        {
            if (answer.User == Position.Keep)
            {
                kept[answer.Id] = clash.Reason;
            }
        }

        return kept;
    }

    /// <summary>
    /// Each problem a clash holds red: each one the user has not judged yet, and each one he answered replace to while
    /// its subject's code is the code he answered about. A replace about a subject that was gone holds nothing. The
    /// finding of a replace is the validator's, with the user's words after it. The code of each held subject is
    /// stored, so a later fix can show it.
    /// </summary>
    public List<Hold> Holds()
    {
        var holds = new List<Hold>();
        foreach (var clash in history.Clashes())
        {
            holds.Add(HoldOf(clash, $"the builder and the validator clash, red until the user judges it in brain's window: {clash.Refusal.WhatIsWrong}"));
        }

        foreach (var (clash, answer) in history.Replaced())
        {
            if (answer.Code is null)
            {
                continue;
            }

            if (CodeNow(clash.Refusal.Symbol) != answer.Code)
            {
                continue;
            }

            var finding = clash.Refusal.Finding;
            if (answer.Words is { } words)
            {
                finding = $"{finding} The user adds: {words}";
            }

            holds.Add(HoldOf(clash, $"the user sided with the validator in brain's window, so the code must change: {finding}"));
        }

        return holds;
    }

    // The hold of a problem in a clash, where its subject is now, or where the builder defended it when the subject is
    // gone.
    private Hold HoldOf(Clash clash, string says)
    {
        var refusal = clash.Refusal;
        if (!subjects.TryGetValue(new SymbolId(refusal.Symbol), out var subject))
        {
            return new Hold(refusal.Id, refusal.Check, refusal.Symbol, clash.Defended.Path, clash.Defended.Line, says, null);
        }

        return new Hold(refusal.Id, refusal.Check, refusal.Symbol, subject.Path, subject.Line, says, subject.Text);
    }

    // The hash of the code of the subject with the given symbol now, or null when it is gone.
    private string? CodeNow(string symbol)
    {
        if (!subjects.TryGetValue(new SymbolId(symbol), out var subject))
        {
            return null;
        }

        return NavaroneEventHandler.HashOf(subject.Text);
    }
}
