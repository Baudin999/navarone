using Navarone.Engine;
using Navarone.Engine.Events;
using Navarone.Engine.Reasons;

namespace Navarone.Cli;

/// <summary>The answers to a problem by its id: the builder's reason, the validator's keep and replace, a large change and the user's decision on it.</summary>
internal sealed class IdAnswers(Repo repo)
{
    /// <summary>`navarone &lt;id&gt; "&lt;reason&gt;"`: the builder's reason.</summary>
    public static InRepo Reason(ProblemId id, string reason)
    {
        return new InRepo("reason", Costed: true, current => new IdAnswers(current).ResolveAsync(id, reason));
    }

    /// <summary>`navarone &lt;id&gt; keep`: the validator accepts.</summary>
    public static InRepo Keep(ProblemId id)
    {
        return new InRepo("keep", Costed: true, current => new IdAnswers(current).KeepAsync(id));
    }

    /// <summary>`navarone &lt;id&gt; replace &lt;rule&gt; "&lt;finding&gt;"`: the validator refuses.</summary>
    public static InRepo Replace(ProblemId id, string rule, string finding)
    {
        return new InRepo("replace", Costed: true, current => new IdAnswers(current).ReplaceAsync(id, rule, finding));
    }

    /// <summary>`navarone &lt;id&gt; large "&lt;text&gt;"`: the real fix is a large change.</summary>
    public static InRepo Large(ProblemId id, string text)
    {
        return new InRepo("large", Costed: true, current => new IdAnswers(current).LargeChangeAsync(id, text));
    }

    /// <summary>`navarone &lt;id&gt; settled keep|replace ["&lt;words&gt;"]`: the user's decision on a large change.</summary>
    public static InRepo Settled(ProblemId id, string choice, string? words)
    {
        return new InRepo("settled", Costed: true, current => new IdAnswers(current).SettleAsync(id, choice, words));
    }

    // `navarone <id> "<reason>"`: stores the reason as that problem's resolution, unless a rule rejects it, and says
    // which.
    private async Task<int> ResolveAsync(ProblemId id, string reason)
    {
        var rejection = await Analysis.ResolveAsync(repo.Setup, repo.Codebases, id, reason, CancellationToken.None).ConfigureAwait(false);
        if (rejection is not null)
        {
            await repo.Output.WriteLineAsync($"{id.Value} rejected: {rejection}").ConfigureAwait(false);
            return ExitCodes.Rejected;
        }

        await repo.Output.WriteLineAsync($"{id.Value} resolved: the problem stays silent while its code is unchanged").ConfigureAwait(false);
        return ExitCodes.Resolved;
    }

    // `navarone <id> keep`: the validator accepts the builder's fix or reason, and Navarone logs it. Neither
    // reasons.json nor a resolution changes.
    private async Task<int> KeepAsync(ProblemId id)
    {
        var builder = await Analysis.KeepAsync(repo.Setup, repo.Codebases, id, CancellationToken.None).ConfigureAwait(false);
        await repo.Output.WriteLineAsync($"{id.Value} keep: the validator accepts the {AnswerOf(builder)}").ConfigureAwait(false);
        return ExitCodes.Recorded;
    }

    // `navarone <id> replace <rule> "<finding>"`: the validator holds that the code must still change, the rule of
    // NAVARONE.md it breaks, by its number there, and why, and Navarone logs it. Neither reasons.json nor a resolution
    // changes. A blank finding, and a rule NAVARONE.md does not number, are usage errors.
    private async Task<int> ReplaceAsync(ProblemId id, string rule, string finding)
    {
        if (string.IsNullOrWhiteSpace(finding))
        {
            await repo.Error.WriteLineAsync("navarone: replace takes the finding: why the code must still change").ConfigureAwait(false);
            return ExitCodes.UsageError;
        }

        var builder = await Analysis.ReplaceAsync(repo.Setup, repo.Codebases, id, rule, finding, CancellationToken.None).ConfigureAwait(false);
        await repo.Output.WriteLineAsync($"{id.Value} replace: the validator refuses the {AnswerOf(builder)}; the code must still change").ConfigureAwait(false);
        return ExitCodes.Recorded;
    }

    // `navarone <id> large "<text>"`: the real fix is a large change, bigger than the task. Navarone stores the text as the
    // problem's answer, so it is green and the task lands, and logs it; the agent names it in its report to the open
    // agent. A blank text is a usage error.
    private async Task<int> LargeChangeAsync(ProblemId id, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            await repo.Error.WriteLineAsync("navarone: large takes what is wrong, where it shows and what the fix is").ConfigureAwait(false);
            return ExitCodes.UsageError;
        }

        await Analysis.LargeChangeAsync(repo.Setup, repo.Codebases, id, text, CancellationToken.None).ConfigureAwait(false);
        await repo.Output.WriteLineAsync($"{id.Value} large: the large change is the problem's answer; name it in your report to the open agent, who brings it to the user").ConfigureAwait(false);
        return ExitCodes.Recorded;
    }

    // `navarone <id> settled keep|replace ["<words>"]`: an agent logs the user's decision on a large change, with his
    // words. A choice other than keep and replace is a usage error, and blank words are no words.
    private async Task<int> SettleAsync(ProblemId id, string choice, string? words)
    {
        var user = DecisionOf(choice);
        if (string.IsNullOrWhiteSpace(words))
        {
            words = null;
        }

        await Analysis.SettleAsync(repo.Setup, repo.Codebases, id, user, words, CancellationToken.None).ConfigureAwait(false);
        await repo.Output.WriteLineAsync($"{id.Value} settled: the user's {choice} is logged").ConfigureAwait(false);
        return ExitCodes.Recorded;
    }

    // The user's decision as an agent writes it: keep or replace. Any other word throws an InvalidDataException.
    private static Position DecisionOf(string choice) => choice switch
    {
        "keep" => Position.Keep,
        "replace" => Position.Replace,
        _ => throw new InvalidDataException($"settled takes the user's keep or replace, not \"{choice}\""),
    };

    // What the builder answered with: a fix replaces the code, a reason keeps it.
    private static string AnswerOf(Position builder)
    {
        if (builder == Position.Replace)
        {
            return "fix";
        }

        return "reason";
    }
}
