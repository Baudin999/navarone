using Navarone.Engine.Reasons;

namespace Navarone.Cli;

/// <summary>The grammar of `navarone`: the arguments, read as one of its forms.</summary>
internal static class Grammar
{
    /// <summary>The form the arguments take. Arguments of no form are the usage, which is a usage error.</summary>
    // navarone: 0000068
    // navarone: 0000070
    public static Command Parse(string[] args)
    {
        return args switch
        {
            [] => CheckCommands.Touched(),
            ["--full-coverage"] => CheckCommands.FullCoverage(),
            ["init"] => InitCommand.Init(),
            ["disputes", "--json"] => BrainCommands.Disputes(),
            ["events", "--json"] => BrainCommands.Events(),
            ["code", var hash] => BrainCommands.Code(hash),
            ["lens", "--json", .. var words] => ResearchCommands.LensJson(words),
            ["lens", .. var words] => ResearchCommands.LensMarkdown(words),
            ["map", "--json"] => ResearchCommands.Map(),
            ["evidence", var id, "--json"] when ProblemId.IsShape(id) => ResearchCommands.Evidence(new ProblemId(id)),
            ["duplicates", .. var options] => ResearchCommands.Duplicates(options),
            [var word, .. var rest] when ProblemId.IsShape(word) => IdForm(new ProblemId(word), rest),
            _ => Usage(),
        };
    }

    // The forms that start with a problem id, told apart by the words after it. Only a word of an id's shape gets here.
    // navarone: 0000069
    // navarone: 0000071
    private static Command IdForm(ProblemId id, string[] rest)
    {
        return rest switch
        {
            ["keep"] => IdAnswers.Keep(id),
            ["replace", var rule, var finding] => IdAnswers.Replace(id, rule, finding),
            ["replace", _] => NoRule(),
            ["replace"] or ["large"] or ["settled"] => Usage(),
            ["large", var text] => IdAnswers.Large(id, text),
            ["settled", var choice] => IdAnswers.Settled(id, choice, null),
            ["settled", var choice, var words] => IdAnswers.Settled(id, choice, words),
            [var reason] => IdAnswers.Reason(id, reason),
            _ => Usage(),
        };
    }

    // `navarone <id> replace "<finding>"` without its rule: a refusal names the rule of NAVARONE.md the code breaks, so
    // the user's question can take its title from it.
    private static Anywhere NoRule()
    {
        return new Anywhere("replace", Costed: false, session => NoRuleAsync(session.Error));
    }

    private static async Task<int> NoRuleAsync(TextWriter error)
    {
        await error.WriteLineAsync("navarone: replace names the rule of NAVARONE.md the code breaks, by its number there: navarone <id> replace <rule> \"<finding>\"").ConfigureAwait(false);
        return ExitCodes.UsageError;
    }

    // Arguments of no form: the usage, on stderr.
    private static Anywhere Usage()
    {
        return new Anywhere("usage", Costed: false, session => UsageAsync(session.Error));
    }

    private static async Task<int> UsageAsync(TextWriter error)
    {
        await error.WriteLineAsync("usage: navarone [--full-coverage]").ConfigureAwait(false);
        await error.WriteLineAsync("       navarone init").ConfigureAwait(false);
        await error.WriteLineAsync("       navarone <id> \"<reason>\"").ConfigureAwait(false);
        await error.WriteLineAsync("       navarone <id> keep").ConfigureAwait(false);
        await error.WriteLineAsync("       navarone <id> replace <rule> \"<finding>\"").ConfigureAwait(false);
        await error.WriteLineAsync("       navarone <id> large \"<what is wrong, where it shows, what the fix is>\"").ConfigureAwait(false);
        await error.WriteLineAsync("       navarone <id> settled keep|replace [\"<words>\"]").ConfigureAwait(false);
        await error.WriteLineAsync("       navarone events --json").ConfigureAwait(false);
        await error.WriteLineAsync("       navarone code <hash>").ConfigureAwait(false);
        await error.WriteLineAsync("       navarone disputes --json").ConfigureAwait(false);
        await error.WriteLineAsync("       navarone evidence <id> --json").ConfigureAwait(false);
        await error.WriteLineAsync("       navarone duplicates [--similarity <share>] [--tokens <count>]").ConfigureAwait(false);
        await error.WriteLineAsync("       navarone map --json").ConfigureAwait(false);
        await error.WriteLineAsync("       navarone lens [--json] method <Type.Method> <view>...").ConfigureAwait(false);
        return ExitCodes.UsageError;
    }
}
