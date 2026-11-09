using System.Collections.Frozen;
using Navarone.Engine.Classification;
using Navarone.Engine.Clashes;
using Navarone.Engine.Events;
using Navarone.Engine.Facts;
using Navarone.Engine.Findings;
using Navarone.Engine.Providers;
using Navarone.Engine.Reasons;

namespace Navarone.Engine;

/// <summary>
/// The engine entry points behind `navarone`, `navarone &lt;id&gt; "&lt;reason&gt;"`, `navarone &lt;id&gt; keep`,
/// `navarone &lt;id&gt; replace &lt;rule&gt; "&lt;finding&gt;"`, `navarone &lt;id&gt; large "&lt;text&gt;"`,
/// `navarone &lt;id&gt; settled` and `navarone evidence`. Each writes the events it makes happen through the setup's
/// event handler. A reason, keep, replace, large and settled, whose product is an event, write it before they
/// save a resolution: a log that cannot be written throws an InvalidDataException and the command stores nothing.
/// </summary>
public static class Analysis
{
    // No problem the user kept, for every check that collects no answers.
    private static readonly IReadOnlyDictionary<string, string> NoneKept = FrozenDictionary<string, string>.Empty;

    /// <summary>
    /// Loads each codebase at the given paths on its own and merges their facts, so a subject two codebases share is
    /// one subject. Runs every check over the facts and matches each finding against reasons.json by its check and its
    /// symbol. A finding that matches no problem by symbol may be a rename: when its code carries the id of a problem
    /// of the same check whose recorded symbol is gone, it is that problem. It keeps its id and takes the new symbol and
    /// fingerprint, so its resolution no longer counts. Any other finding that matches no problem is new: Navarone
    /// grants it an id. Every problem takes the finding's fingerprint and value. It collects the user's judgement of
    /// each clash on this worktree first, as WorktreeClashes says, and a problem he kept takes the builder's reason as
    /// its resolution. Removes every problem whose code is gone and saves
    /// reasons.json. Returns one verdict per finding the given coverage shows, but for the problems a clash holds
    /// red, which it returns apart, whatever the coverage: a finding is shown when the coverage covers a line of its
    /// subject's code, or of a helper's. See CheckedRepo.VerdictOf for what turns a problem green.
    /// The run also holds what it made happen on this worktree, which LogRun writes: a raised for each id granted and
    /// for each shown red problem that is not open on this worktree, a settled for each answer collected, and a fixed
    /// for each open problem that has no finding now and whose code changed. WorktreeHistory says when a problem is
    /// open. It stores the code of each shown red problem and each held one, so a later fix can show the code before it.
    /// </summary>
    // navarone: 0000042
    public static async Task<CheckRun> RunAsync(
        EngineSetup setup,
        IReadOnlyList<string> paths,
        Coverage coverage,
        CancellationToken cancellationToken)
    {
        var (facts, subjects) = await LoadAsync(setup, paths, cancellationToken).ConfigureAwait(false);
        var place = EventPlace.Of(setup.RepoRoot);
        var history = WorktreeHistory.Of(setup.Events.Read(), place.Worktree);
        var written = subjects.ToDictionary(pair => pair.Key, pair => pair.Value.Written);
        var clashes = new WorktreeClashes(history, written);
        var judged = clashes.Collect(setup.Answers);
        var kept = WorktreeClashes.Kept(judged);
        var found = CheckedRepo.Of(setup, facts, subjects, kept);
        found.Save();
        var holds = clashes.Holds(setup.Events);
        var held = holds.Select(hold => hold.Id).ToHashSet(StringComparer.Ordinal);
        var shown = found.Problems.Where(problem => problem.ShownBy(coverage)).ToList();
        var free = shown.Where(problem => !held.Contains(problem.Verdict.Problem.Id.Value)).ToList();

        var happened = new List<INavaroneEvent>(found.Granted);
        happened.AddRange(judged.Select(each => each.Answer));
        happened.AddRange(RunEvents.Fixes(setup.Events, found, history));
        happened.AddRange(RunEvents.RaisedAgain(shown, found.Granted, history));
        var entries = new List<ShownProblem>();
        var verdicts = new List<Verdict>();
        foreach (var problem in free)
        {
            entries.Add(RunEvents.Entry(setup.Events, problem));
            verdicts.Add(problem.Verdict);
        }

        foreach (var hold in holds)
        {
            entries.Add(new ShownProblem(hold.Id, hold.Check, hold.Symbol, FindingState.Red, hold.Code));
        }

        return new CheckRun(verdicts, place, happened, entries, holds);
    }

    /// <summary>Writes what the given run made happen, then the run itself, with its cost in tokens, to the event log.</summary>
    public static void LogRun(EngineSetup setup, CheckRun run, int tokens) =>
        setup.Events.Write(run.Place, [.. run.Happened, new RunEvent(run.Shown, tokens)]);

    /// <summary>
    /// Runs `navarone` over the codebases at the given paths, then finds the problem with the given id among its
    /// findings. The reason classifier rules on the given reason. A reason no rule rejects is stored as the problem's
    /// resolution, with the fingerprint and value of the code it answers, in place of any resolution before it, and
    /// logged with the code it defends. A problem with no finding now takes a reason only while the validator's refusal
    /// of its fix stands on this worktree: the reason argues for the fix, so it is logged and stored nowhere, and its
    /// code need not carry its id. Returns why the reason was rejected, or null once it is logged. An id that matches no
    /// problem in the code as it is now, and none with a refused fix, throws an InvalidDataException, and nothing is
    /// stored.
    /// </summary>
    public static async Task<string?> ResolveAsync(
        EngineSetup setup,
        IReadOnlyList<string> paths,
        ProblemId id,
        string reason,
        CancellationToken cancellationToken)
    {
        var found = await CheckAsync(setup, paths, cancellationToken).ConfigureAwait(false);
        var place = EventPlace.Of(setup.RepoRoot);
        setup.Events.Write(place, found.Granted);
        var answered = found.ProblemOf(id);
        if (answered is null)
        {
            return ArgueFix(setup, found, place, id, reason);
        }

        var recorded = answered.Verdict.Problem;
        var reasonCase = new ReasonCase(answered.Verdict.Finding, recorded, answered.IdsAtCode, reason, found.RecordedProblems);
        if (setup.ReasonClassifier.Rejection(reasonCase) is { } rejection)
        {
            return rejection;
        }

        var defended = found.Defended(setup.Events, recorded.Symbol, answered.Verdict.Finding.Location.Line);
        setup.Events.WriteOrThrow(place, [new ResolvedEvent(id.Value, reason, defended)]);
        found.Resolve(answered, reason);
        found.Save();
        return null;
    }

    // The builder's reason for a fixed problem, which has no finding now, against the validator's refusal of its fix:
    // the reason classifier rules on it with no finding, and a reason no rule rejects is logged with the code it
    // defends and stored nowhere. A problem with no refused fix standing on this worktree, or whose subject is gone,
    // throws an InvalidDataException.
    private static string? ArgueFix(EngineSetup setup, CheckedRepo found, EventPlace place, ProblemId id, string reason)
    {
        var history = WorktreeHistory.Of(setup.Events.Read(), place.Worktree);
        var refusal = history.StandingRefusal(id.Value);
        if (refusal?.Before is null)
        {
            throw new InvalidDataException($"no problem with id {id.Value} in the code as it is now, and no refused fix of it on this worktree");
        }

        var standing = found.RecordedOf(id);
        if (standing is null)
        {
            throw new InvalidDataException($"no problem with id {id.Value} in the code as it is now");
        }

        var recorded = standing.Problem;
        var subject = standing.Subject;

        var reasonCase = new ReasonCase(null, recorded, subject.Ids, reason, found.RecordedProblems);
        if (setup.ReasonClassifier.Rejection(reasonCase) is { } rejection)
        {
            return rejection;
        }

        var defended = found.Defended(setup.Events, recorded.Symbol, subject.Places[0].Line);
        setup.Events.WriteOrThrow(place, [new ResolvedEvent(id.Value, reason, defended)]);
        return null;
    }

    /// <summary>
    /// `navarone &lt;id&gt; keep`: the validator accepts the builder's answer to the problem with the given id, its fix
    /// or its reason, and Navarone logs an accepted. Returns the builder's position: replace for a fix, keep for a
    /// reason. It changes neither reasons.json nor a resolution. See AnswerAsync for when there is nothing to accept.
    /// </summary>
    public static async Task<Position> KeepAsync(
        EngineSetup setup,
        IReadOnlyList<string> paths,
        ProblemId id,
        CancellationToken cancellationToken)
    {
        var (place, answer) = await AnswerAsync(setup, paths, id, cancellationToken).ConfigureAwait(false);
        setup.Events.WriteOrThrow(place, [new AcceptedEvent(id.Value, answer.Builder)]);
        return answer.Builder;
    }

    /// <summary>
    /// `navarone &lt;id&gt; replace &lt;rule&gt; "&lt;finding&gt;"`: the validator holds that the code of the problem
    /// with the given id must still change, because it breaks the rule of the user's taste with the given number in
    /// the NAVARONE.md at the repo root when the repo has one, and otherwise in the setup's global one, for the given
    /// finding. Navarone logs a refused-fix with the code before and after the
    /// fix, or a refused-reason with the code the reason defends, each with the rule. Returns the builder's position:
    /// replace for a fix, keep for a reason. It changes neither reasons.json nor a resolution. A NAVARONE.md that
    /// cannot be read or numbers no rule, and a number no rule has, throw an InvalidDataException; see AnswerAsync for
    /// when there is nothing to refuse.
    /// </summary>
    public static async Task<Position> ReplaceAsync(
        EngineSetup setup,
        IReadOnlyList<string> paths,
        ProblemId id,
        string rule,
        string finding,
        CancellationToken cancellationToken)
    {
        var broken = TasteRules.For(setup.RepoRoot, setup.Taste).Of(rule);
        var (place, answer) = await AnswerAsync(setup, paths, id, cancellationToken).ConfigureAwait(false);
        var refusal = answer.Refuse(id.Value, broken, finding, setup.Events);
        setup.Events.WriteOrThrow(place, [refusal]);
        return answer.Builder;
    }

    /// <summary>
    /// `navarone &lt;id&gt; large "&lt;text&gt;"`: the real fix of the problem with the given id is a large change, bigger
    /// than the task. Checks the repo as `navarone` does, stores the text as the problem's resolution, so the problem
    /// is green and the task lands, and logs a large with the text and the subject's code, stored. The agent names
    /// it in its report to the open agent. A fixed problem, which has no finding now, needs no resolution. An id
    /// Navarone never granted, or one whose subject is gone from the code, throws an InvalidDataException, and so does
    /// a problem with a finding now whose code does not carry its id: a resolution turns a problem green only where its
    /// code carries its id.
    /// </summary>
    public static async Task LargeChangeAsync(
        EngineSetup setup,
        IReadOnlyList<string> paths,
        ProblemId id,
        string text,
        CancellationToken cancellationToken)
    {
        var found = await CheckAsync(setup, paths, cancellationToken).ConfigureAwait(false);
        var place = EventPlace.Of(setup.RepoRoot);
        setup.Events.Write(place, found.Granted);
        if (!found.HasGranted(id))
        {
            throw new InvalidDataException($"Navarone never granted the id {id.Value}");
        }

        var standing = found.RecordedOf(id);
        if (standing is null)
        {
            throw new InvalidDataException($"no problem with id {id.Value} in the code as it is now");
        }

        var recorded = standing.Problem;
        var subject = standing.Subject;

        var now = found.ProblemOf(id);
        if (now is not null)
        {
            if (!now.IdsAtCode.Contains(id.Value))
            {
                throw new InvalidDataException($"put `// navarone: {id.Value}` at the code first: the large change is the problem's answer only where its code carries its id");
            }
        }

        var code = setup.Events.Store(subject.Text);
        setup.Events.WriteOrThrow(place, [new LargeChangeEvent(id.Value, recorded.Check, recorded.Symbol.Value, text, code)]);
        if (now is not null)
        {
            found.Resolve(now, text);
            found.Save();
        }
    }

    /// <summary>
    /// `navarone &lt;id&gt; settled keep|replace "&lt;words&gt;"`: the user decided the large change of the problem with
    /// the given id, which the open agent brought him, and an agent logs his decision and his words, or null when he
    /// gave none, as a settled with the subject's code as it is now. A keep with words adds them to the agent's text in
    /// the problem's resolution. A replace opens the problem on that code, so the large change's fix is logged as
    /// fixed. A problem no large change of this repo names throws an InvalidDataException,
    /// and so does one in a clash on this worktree: the user judges a clash in brain's window, and the next run
    /// collects his answer.
    /// </summary>
    // navarone: 0000051
    // navarone: 0000052
    public static async Task SettleAsync(
        EngineSetup setup,
        IReadOnlyList<string> paths,
        ProblemId id,
        Position user,
        string? words,
        CancellationToken cancellationToken)
    {
        var place = EventPlace.Of(setup.RepoRoot);
        var logged = setup.Events.Read();
        LargeChangeEvent? asked = null;
        foreach (var each in logged)
        {
            if (each.Place.Repo != place.Repo)
            {
                continue;
            }

            if (each.Event is not LargeChangeEvent large)
            {
                continue;
            }

            if (large.Id != id.Value)
            {
                continue;
            }

            asked = large;
        }

        if (asked is null)
        {
            throw new InvalidDataException($"no large change of {id.Value} is logged in this repo: settled logs the user's decision on a large change");
        }

        var history = WorktreeHistory.Of(logged, place.Worktree);
        if (history.Clashes().Any(clash => clash.Refusal.Id == id.Value))
        {
            throw new InvalidDataException($"{id.Value} is in a clash: the user judges it in brain's window, and the next `navarone` collects his answer");
        }

        var found = await CheckAsync(setup, paths, cancellationToken).ConfigureAwait(false);
        setup.Events.Write(place, found.Granted);
        string? code = null;
        var subject = found.SubjectOf(new SymbolId(asked.Symbol));
        if (subject is not null)
        {
            code = setup.Events.Store(subject.Text);
        }

        setup.Events.WriteOrThrow(place, [new SettledEvent(id.Value, asked.Check, asked.Symbol, user, words, code)]);
        if (user == Position.Keep)
        {
            AddWords(found, id, asked.Text, words);
        }
    }

    // The user's keep of a large change with words: the problem's resolution becomes the agent's text with his words
    // after it, for the code as it is now. Without words, or for a problem with no finding now, the resolution stays.
    private static void AddWords(CheckedRepo found, ProblemId id, string text, string? words)
    {
        if (words is null)
        {
            return;
        }

        var now = found.ProblemOf(id);
        if (now is null)
        {
            return;
        }

        found.Resolve(now, $"{text} The user adds: {words}");
        found.Save();
    }

    /// <summary>
    /// The finding of the problem with the given id in the codebases at the given paths, from the code as it is now,
    /// for `navarone evidence &lt;id&gt; --json`. Read-only: it grants no id and never writes reasons.json. Returns null
    /// when reasons.json holds no problem with that id whose check finds it in the code now.
    /// </summary>
    public static async Task<Finding?> FindingOfAsync(
        EngineSetup setup,
        IReadOnlyList<string> paths,
        ProblemId id,
        CancellationToken cancellationToken)
    {
        var (facts, subjects) = await LoadAsync(setup, paths, cancellationToken).ConfigureAwait(false);
        var reasons = ReasonsFile.Load(setup.RepoRoot);
        foreach (var measured in CheckedRepo.Measured(setup, facts, subjects))
        {
            if (reasons.For(measured.Finding)?.Id == id)
            {
                return measured.Finding;
            }
        }

        return null;
    }

    // How the builder answered the problem with the given id on this worktree, for the validator's keep or replace, read
    // from the code as it is now and the event log without writing either. A problem that has a finding now was answered
    // with a reason when it is green by its resolution. A problem with no finding now was answered with the last fix of
    // it logged on this worktree. An id Navarone never granted, a problem that is red, a false positive, and a problem
    // with no finding and no fix logged, have no answer to accept or refuse: each throws an InvalidDataException.
    private static async Task<(EventPlace Place, BuilderAnswer Answer)> AnswerAsync(
        EngineSetup setup,
        IReadOnlyList<string> paths,
        ProblemId id,
        CancellationToken cancellationToken)
    {
        if (!ReasonsFile.Load(setup.RepoRoot).HasGranted(id))
        {
            throw new InvalidDataException($"Navarone never granted the id {id.Value}");
        }

        var (facts, subjects) = await LoadAsync(setup, paths, cancellationToken).ConfigureAwait(false);
        var found = CheckedRepo.Of(setup, facts, subjects, NoneKept);
        var place = EventPlace.Of(setup.RepoRoot);
        var now = found.ProblemOf(id);
        if (now is not null)
        {
            return (place, BuilderAnswer.Now(now, id));
        }

        var history = WorktreeHistory.Of(setup.Events.Read(), place.Worktree);
        if (history.LastFix(id.Value) is not { } fix)
        {
            throw new InvalidDataException($"{id.Value} has no finding in the code as it is now and no fix on this worktree: there is nothing to accept or refuse");
        }

        return (place, new FixAnswer(fix));
    }

    // What ResolveAsync, LargeChangeAsync and SettleAsync check, saved: reasons.json with every problem's id,
    // fingerprint and value, and the problems whose code is gone removed.
    private static async Task<CheckedRepo> CheckAsync(
        EngineSetup setup,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var (facts, subjects) = await LoadAsync(setup, paths, cancellationToken).ConfigureAwait(false);
        var found = CheckedRepo.Of(setup, facts, subjects, NoneKept);
        found.Save();
        return found;
    }

    // The merged facts of every codebase and each subject in them.
    private static async Task<(CodebaseFacts Facts, Dictionary<SymbolId, Subject> Subjects)> LoadAsync(
        EngineSetup setup,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var merged = await new Code(setup.Provider, setup.RepoRoot, paths).FactsAsync(cancellationToken).ConfigureAwait(false);
        return (merged, Subject.AllIn(merged));
    }
}
