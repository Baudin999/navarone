using Navarone.Engine.Reasons;

namespace Navarone.Engine.Events;

/// <summary>
/// What the log says of each problem on one worktree: whether it is open, the last fix of it, and whether the validator
/// ever refused an answer for it. A problem is open from a run that shows it red, with the code of that run, and from a
/// validator's refusal, with the code it refused: the refused fix's code after, or the code the refused reason
/// defends. A fix closes it. A run that shows it green closes it too, unless a refusal opened it: a refused reason
/// still turns the problem green until the builder changes its code. Ids are granted per reasons.json, so a problem
/// is known by its id within one worktree only.
/// It also holds each clash the user has not judged yet, the latest of each problem, and each clash he answered replace
/// to. A clash is a refusal that names its rule, answered with a new reason for the code refused: the builder holds its
/// position. A reason for other code answers the refusal by changing the code, and the validator's keep ends the
/// refusal. While a clash waits, a run that shows its problem neither opens nor closes it: it only names the subject's
/// code for the question. Any settled ends a clash, and a replace opens the problem on the code the user decided
/// about, as a refusal does, so the fix of it is logged.
/// </summary>
internal sealed class WorktreeHistory
{
    // Each open problem, with the code it was open on.
    private readonly Dictionary<string, ShownProblem> open = new(StringComparer.Ordinal);

    // The problems a refusal opened and no fix has closed since.
    private readonly HashSet<string> reopened = new(StringComparer.Ordinal);

    private readonly Dictionary<string, FixedEvent> lastFix = new(StringComparer.Ordinal);

    private readonly HashSet<string> refused = new(StringComparer.Ordinal);

    // The validator's last refusal of each problem that names its rule, until the builder changes the code or the
    // validator keeps.
    private readonly Dictionary<string, Refusal> standing = new(StringComparer.Ordinal);

    private readonly Dictionary<string, Clash> clashes = new(StringComparer.Ordinal);

    // Each clash the user answered replace to, with his answer, until a fix closes it.
    private readonly Dictionary<string, (Clash Clash, SettledEvent Answer)> replaced = new(StringComparer.Ordinal);

    private WorktreeHistory()
    {
    }

    /// <summary>The history of the worktree at the given path, from the given events in the order written.</summary>
    public static WorktreeHistory Of(IReadOnlyList<LoggedEvent> events, string worktree) =>
        OfEach(events).GetValueOrDefault(worktree) ?? new WorktreeHistory();

    /// <summary>The history of every worktree the given events name, by its path.</summary>
    // navarone: 0000045
    public static Dictionary<string, WorktreeHistory> OfEach(IReadOnlyList<LoggedEvent> events)
    {
        var histories = new Dictionary<string, WorktreeHistory>(StringComparer.Ordinal);
        foreach (var logged in events)
        {
            if (!histories.TryGetValue(logged.Place.Worktree, out var history))
            {
                history = new WorktreeHistory();
                histories.Add(logged.Place.Worktree, history);
            }

            history.Add(logged);
        }

        return histories;
    }

    /// <summary>Each open problem, with the code it was open on.</summary>
    public IEnumerable<ShownProblem> Open() => open.Values;

    /// <summary>Whether the problem with the given id is open.</summary>
    public bool IsOpen(string id) => open.ContainsKey(id);

    /// <summary>Whether the validator refused a fix or a reason for the problem with the given id.</summary>
    public bool Refused(string id) => refused.Contains(id);

    /// <summary>The last fix of the problem with the given id, or null when none is logged.</summary>
    public FixedEvent? LastFix(string id) => lastFix.GetValueOrDefault(id);

    /// <summary>The validator's standing refusal of the problem with the given id, or null when none stands.</summary>
    public Refusal? StandingRefusal(string id) => standing.GetValueOrDefault(id);

    /// <summary>
    /// Each clash the user has not judged yet, with the subject's code as the last run since showed it, which every run
    /// does, since a clash holds its problem red.
    /// </summary>
    public IReadOnlyList<Clash> Clashes() => [.. clashes.Values];

    /// <summary>Each clash the user answered replace to and no fix has closed since, with his answer.</summary>
    public IReadOnlyList<(Clash Clash, SettledEvent Answer)> Replaced() => [.. replaced.Values];

    /// <summary>
    /// The user's decision on a problem, collected now or logged: it ends the problem's clash and its standing refusal.
    /// A replace opens the problem on the code he decided about, the code the decision names, as a refusal does,
    /// without counting as a refusal, and a replace on a clash holds the clash until the fix.
    /// </summary>
    public void Settle(SettledEvent settled)
    {
        standing.Remove(settled.Id);
        clashes.Remove(settled.Id, out var clash);
        if (settled.User == Position.Keep)
        {
            return;
        }

        if (clash is not null)
        {
            replaced[settled.Id] = (clash, settled);
        }

        open[settled.Id] = new ShownProblem(settled.Id, settled.Check, settled.Symbol, FindingState.Red, settled.Code);
        reopened.Add(settled.Id);
    }

    private void Add(LoggedEvent logged)
    {
        switch (logged.Event)
        {
            case RunEvent run:
                foreach (var shown in run.Shown)
                {
                    Show(shown);
                }

                break;
            case FixedEvent fix:
                open.Remove(fix.Id);
                reopened.Remove(fix.Id);
                replaced.Remove(fix.Id);
                standing.Remove(fix.Id);
                lastFix[fix.Id] = fix;
                break;
            case ResolvedEvent resolved:
                Answer(resolved, logged);
                break;
            case AcceptedEvent accepted:
                standing.Remove(accepted.Id);
                clashes.Remove(accepted.Id);
                break;
            case SettledEvent settled:
                Settle(settled);
                break;
            case RefusedFixEvent refusedFix:
                Reopen(new ShownProblem(refusedFix.Id, refusedFix.Check, refusedFix.Symbol, FindingState.Red, refusedFix.After));
                Stand(refusedFix.Id, Refusal.Of(refusedFix));
                break;
            case RefusedReasonEvent refusedReason:
                Reopen(new ShownProblem(refusedReason.Id, refusedReason.Check, refusedReason.Symbol, FindingState.Red, refusedReason.Before));
                Stand(refusedReason.Id, Refusal.Of(refusedReason));
                break;
        }
    }

    // The builder's reason against a standing refusal: a clash when it defends the code refused, unchanged, and an
    // answer that ends the refusal when it defends other code.
    private void Answer(ResolvedEvent reason, LoggedEvent logged)
    {
        if (!standing.TryGetValue(reason.Id, out var refusal))
        {
            return;
        }

        // A reason logged before Navarone logged the code it defends cannot be told from a changed one.
        if (reason.Defended is not { } defended)
        {
            return;
        }

        if (defended.Code != refusal.Code)
        {
            standing.Remove(reason.Id);
            return;
        }

        clashes[reason.Id] = new Clash(refusal, reason.Reason, defended, logged.At, logged.Place, defended.Code);
        replaced.Remove(reason.Id);
    }

    // A refusal that names its rule stands until the builder changes the code; one that names none ends the refusal
    // before it.
    private void Stand(string id, Refusal? refusal)
    {
        if (refusal is null)
        {
            standing.Remove(id);
            return;
        }

        standing[id] = refusal;
    }

    private void Show(ShownProblem shown)
    {
        if (clashes.TryGetValue(shown.Id, out var clash))
        {
            clashes[shown.Id] = clash with { CodeNow = shown.Code };
            return;
        }

        if (shown.State == FindingState.Red)
        {
            open[shown.Id] = shown;
            return;
        }

        if (!reopened.Contains(shown.Id))
        {
            open.Remove(shown.Id);
        }
    }

    private void Reopen(ShownProblem refusedCode)
    {
        open[refusedCode.Id] = refusedCode;
        reopened.Add(refusedCode.Id);
        refused.Add(refusedCode.Id);
    }
}
