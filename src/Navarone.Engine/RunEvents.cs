using Navarone.Engine.Events;
using Navarone.Engine.Facts;
using Navarone.Engine.Reasons;

namespace Navarone.Engine;

/// <summary>
/// What a `navarone` run made happen on its worktree, beside the ids it granted and the answers it collected: a fixed
/// for each open problem the builder fixed, a raised for each shown red problem that is not open, and the run's entry
/// for each problem it shows.
/// </summary>
internal static class RunEvents
{
    /// <summary>
    /// A fixed for each open problem that has no finding now and whose subject's code differs from the code it was open
    /// on: the builder changed the code because of Navarone. The code after is its subject's code now, or none when the
    /// subject is gone. A fix after a refusal is a second round.
    /// </summary>
    public static List<FixedEvent> Fixes(NavaroneEventHandler events, CheckedRepo found, WorktreeHistory history)
    {
        var now = found.Problems.Select(problem => problem.Verdict.Problem.Id.Value).ToHashSet(StringComparer.Ordinal);
        var fixes = new List<FixedEvent>();
        foreach (var open in history.Open())
        {
            if (now.Contains(open.Id))
            {
                continue;
            }

            // A refused fix that left no subject has no code before to show a later fix against.
            if (open.Code is not { } before)
            {
                continue;
            }

            // Code the problem is open on is stored already, so storing the code now costs nothing when it is unchanged.
            string? after = null;
            var subject = found.SubjectOf(new SymbolId(open.Symbol));
            if (subject is not null)
            {
                after = events.Store(subject.Text);
            }

            if (after == before)
            {
                continue;
            }

            fixes.Add(new FixedEvent(open.Id, open.Check, open.Symbol, before, after, history.Refused(open.Id)));
        }

        return fixes;
    }

    /// <summary>
    /// A raised for each shown red problem that is not open on this worktree, unless this run granted its id: it was
    /// green or gone, or this worktree's runs never showed it.
    /// </summary>
    public static List<RaisedEvent> RaisedAgain(List<CheckedProblem> shown, IReadOnlyList<RaisedEvent> granted, WorktreeHistory history)
    {
        var grantedIds = granted.Select(raised => raised.Id).ToHashSet(StringComparer.Ordinal);
        var raised = new List<RaisedEvent>();
        foreach (var problem in shown)
        {
            var recorded = problem.Verdict.Problem;
            if (problem.Verdict.State != FindingState.Red)
            {
                continue;
            }

            if (grantedIds.Contains(recorded.Id.Value))
            {
                continue;
            }

            if (history.IsOpen(recorded.Id.Value))
            {
                continue;
            }

            raised.Add(new RaisedEvent(recorded.Id.Value, recorded.Check, recorded.Symbol.Value));
        }

        return raised;
    }

    /// <summary>A shown problem as its run's entry. A red one's subject code is stored, so a later fix can show the code before it.</summary>
    public static ShownProblem Entry(NavaroneEventHandler events, CheckedProblem problem)
    {
        var recorded = problem.Verdict.Problem;
        string? code = null;
        if (problem.Verdict.State == FindingState.Red)
        {
            code = events.Store(problem.Text);
        }

        return new ShownProblem(recorded.Id.Value, recorded.Check, recorded.Symbol.Value, problem.Verdict.State, code);
    }
}
