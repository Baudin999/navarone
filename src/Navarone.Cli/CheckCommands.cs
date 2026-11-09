using System.Text;
using Navarone.Engine;
using Navarone.Engine.Reasons;

namespace Navarone.Cli;

/// <summary>The check: `navarone` over the code the change touches, and `navarone --full-coverage` over every problem.</summary>
internal sealed class CheckCommands(Repo repo)
{
    /// <summary>`navarone`: the check over the code the change touches.</summary>
    public static InRepo Touched()
    {
        return new InRepo("check", Costed: false, current => new CheckCommands(current).TouchedAsync());
    }

    /// <summary>`navarone --full-coverage`: the check over every problem in the repo.</summary>
    public static InRepo FullCoverage()
    {
        return new InRepo("check", Costed: false, current => new CheckCommands(current).CheckAsync(Coverage.Full));
    }

    // `navarone`: the check over the code the change since the default branch touches. Reading the change from git
    // throws an InvalidDataException when there is no commit to measure it from, and when a git diff or ls-files fails
    // or times out.
    private async Task<int> TouchedAsync()
    {
        var coverage = Coverage.Touched(repo.Setup.RepoRoot, repo.Codebases);
        return await CheckAsync(coverage).ConfigureAwait(false);
    }

    // `navarone` and `navarone --full-coverage`: checks the repo it runs in, grants ids to new problems and prints each
    // red problem the coverage shows as a question with its id, and each problem a clash holds red, whatever the
    // coverage, with what holds it. Only those count toward the exit code. Then it logs the run, with its cost in
    // tokens: the characters it printed divided by four.
    private async Task<int> CheckAsync(Coverage coverage)
    {
        var run = await Analysis.RunAsync(repo.Setup, repo.Codebases, coverage, CancellationToken.None).ConfigureAwait(false);
        var red = new List<(string Id, string Line)>();
        foreach (var verdict in run.Verdicts)
        {
            if (verdict.State != FindingState.Red)
            {
                continue;
            }

            var problem = verdict.Problem;
            var location = verdict.Finding.Location;
            red.Add((problem.Id.Value, $"{problem.Id.Value} {problem.Check} {problem.Symbol.Value} at {location.Path}:{location.Line}: {verdict.Finding.Question}"));
        }

        foreach (var hold in run.Holds)
        {
            red.Add((hold.Id, $"{hold.Id} {hold.Check} {hold.Symbol} at {hold.Path}:{hold.Line}: {hold.Says}"));
        }

        var printed = new StringBuilder();
        foreach (var (_, line) in red.OrderBy(each => each.Id, StringComparer.Ordinal))
        {
            printed.Append(line);
            printed.Append(repo.Output.NewLine);
        }

        await repo.Output.WriteAsync(printed.ToString()).ConfigureAwait(false);
        var tokens = Tokens.Of(printed.Length);
        Analysis.LogRun(repo.Setup, run, tokens);
        if (red.Count > 0)
        {
            return ExitCodes.AnyRed;
        }

        return ExitCodes.AllGreen;
    }
}
