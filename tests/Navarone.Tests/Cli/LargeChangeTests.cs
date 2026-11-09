using Navarone.Cli;
using Navarone.Engine;
using Navarone.Engine.Events;
using Navarone.Engine.Reasons;
using Navarone.Tests.Support;

namespace Navarone.Tests.Cli;

/// <summary>
/// `navarone &lt;id&gt; large "&lt;text&gt;"`: a large change is the problem's answer, so the task lands, and it is no
/// question in brain's window. `navarone &lt;id&gt; settled` logs the user's decision, which the open agent brought
/// him. Each test logs to a temporary data folder.
/// </summary>
public sealed class LargeChangeTests : IDisposable
{
    private const string Text = "One method holds two jobs, the parse and the check. It shows in First. Split the type in two.";

    private readonly TempRepo repo = new();
    private readonly TempEvents events = new();
    private readonly FakeProvider provider = new();
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public LargeChangeTests() => Directory.CreateDirectory(Path.Combine(repo.Root, ".git"));

    public void Dispose()
    {
        output.Dispose();
        error.Dispose();
        repo.Dispose();
        events.Dispose();
    }

    [Fact]
    public async Task LargeLogsTheLargeChangeAndTheProblemIsGreenWithItAsItsAnswer()
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("--full-coverage");
        output.GetStringBuilder().Clear();

        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "large", Text));

        Assert.Equal("0000001 large: the large change is the problem's answer; name it in your report to the open agent, who brings it to the user", output.ToString().TrimEnd());
        var large = Assert.IsType<LargeChangeEvent>(Logged()[^1].Event);
        Assert.Equal(new LargeChangeEvent("0000001", "cyclomatic", "First", Text, NavaroneEventHandler.HashOf("first")), large);
        Assert.Equal("first", events.Handler.Code(large.Code));
        Assert.Equal(Text, ReasonsFile.Load(repo.Root).For(new ProblemId("0000001"))!.Resolution!.Reason);
        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
        Assert.Equal("""{"source":"navarone","questions":[]}""", await DisputesAsync());
    }

    [Fact]
    public async Task AFixedProblemWhoseFindingIsGoneCanBeALargeChangeWithoutItsIdAtTheCode()
    {
        Codebase(("First", 11, "first"));
        await RunAsync("--full-coverage");
        Codebase(("First", 2, "first split"));
        await RunAsync("--full-coverage");

        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "large", Text));

        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
        Assert.Equal(new LargeChangeEvent("0000001", "cyclomatic", "First", Text, NavaroneEventHandler.HashOf("first split")), Logged().Select(each => each.Event).OfType<LargeChangeEvent>().Single());
    }

    [Fact]
    public async Task ASecondBuildersSettledReplaceOpensTheProblemSoTheBigChangesFixIsLogged()
    {
        Directory.Delete(Path.Combine(repo.Root, ".git"));
        repo.Git("init", "-q", "-b", "master");
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("0000001", "large", Text);
        repo.Git("add", "a.sln", ReasonsFile.FileName);
        repo.Commit();
        var second = Path.Combine(Path.GetTempPath(), $"navarone-second-{Guid.NewGuid():N}");
        repo.Git("worktree", "add", "-q", second, "-b", "big");
        try
        {
            var solution = Path.Combine(second, "a.sln");
            provider.Codebases[solution] = provider.Codebases[Path.Combine(repo.Root, "a.sln")];
            output.GetStringBuilder().Clear();

            Assert.Equal(ExitCodes.Recorded, await RunInAsync(second, "0000001", "settled", "replace", "make the parse a type of its own"));

            Assert.Equal("0000001 settled: the user's replace is logged", output.ToString().TrimEnd());
            var settled = Assert.IsType<SettledEvent>(Logged()[^1].Event);
            Assert.Equal(new SettledEvent("0000001", "cyclomatic", "First", Position.Replace, "make the parse a type of its own", NavaroneEventHandler.HashOf("first")), settled);
            Assert.Equal(second, Logged()[^1].Place.Worktree);
            Assert.Equal(repo.Root, Logged()[^1].Place.Repo);
            // The code changes, so the facts cache of the worktree misses.
            provider.Codebases[solution] = FakeFacts.With(("First", 2, "first big"));
            await File.WriteAllTextAsync(solution, "first big", TestContext.Current.CancellationToken);

            Assert.Equal(ExitCodes.AllGreen, await RunInAsync(second, "--full-coverage"));

            var fix = Assert.Single(Logged().Select(each => each.Event).OfType<FixedEvent>());
            Assert.Equal(new FixedEvent("0000001", "cyclomatic", "First", NavaroneEventHandler.HashOf("first"), NavaroneEventHandler.HashOf("first big"), false), fix);
        }
        finally
        {
            repo.Git("worktree", "remove", "--force", second);
        }
    }

    [Fact]
    public async Task ASettledKeepWithoutWordsLogsTheUsersNoAndTheProblemStaysGreen()
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("0000001", "large", Text);

        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "settled", "keep", " "));

        Assert.Equal(new SettledEvent("0000001", "cyclomatic", "First", Position.Keep, null, NavaroneEventHandler.HashOf("first")), Logged()[^1].Event);
        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
        Assert.Equal(Text, ReasonsFile.Load(repo.Root).For(new ProblemId("0000001"))!.Resolution!.Reason);
    }

    [Fact]
    public async Task ASettledKeepWithWordsAddsThemToTheAgentsTextInTheResolution()
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("0000001", "large", Text);

        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "settled", "keep", "the two jobs share every field"));

        Assert.Equal(new SettledEvent("0000001", "cyclomatic", "First", Position.Keep, "the two jobs share every field", NavaroneEventHandler.HashOf("first")), Logged()[^1].Event);
        Assert.Equal($"{Text} The user adds: the two jobs share every field", ReasonsFile.Load(repo.Root).For(new ProblemId("0000001"))!.Resolution!.Reason);
        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
    }

    [Fact]
    public async Task AProblemWhoseCodeIsGoneStaysOutOfReasonsAfterALargeChange()
    {
        Codebase(["0000001"], ("First", 11, "first"), ("Second", 11, "second"));
        await RunAsync("--full-coverage");
        Codebase(["0000001"], ("First", 11, "first"));

        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "large", Text));

        Assert.Null(ReasonsFile.Load(repo.Root).For(new ProblemId("0000002")));
    }

    [Fact]
    public async Task AProblemWhoseCodeIsGoneStaysOutOfReasonsAfterASettledKeepWithWords()
    {
        Codebase(["0000001"], ("First", 11, "first"), ("Second", 11, "second"));
        await RunAsync("0000001", "large", Text);
        Codebase(["0000001"], ("First", 11, "first"));

        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "settled", "keep", "the two jobs share every field"));

        Assert.Null(ReasonsFile.Load(repo.Root).For(new ProblemId("0000002")));
    }

    [Theory]
    [InlineData(new[] { "0000001", "large", " " }, "navarone: large takes what is wrong, where it shows and what the fix is")]
    [InlineData(new[] { "0000001", "large" }, "usage: navarone [--full-coverage]")]
    [InlineData(new[] { "0000009", "large", Text }, "navarone: Navarone never granted the id 0000009")]
    [InlineData(new[] { "0000002", "large", Text }, "navarone: put `// navarone: 0000002` at the code first: the large change is the problem's answer only where its code carries its id")]
    [InlineData(new[] { "0000001", "settled", "keep" }, "navarone: no large change of 0000001 is logged in this repo: settled logs the user's decision on a large change")]
    [InlineData(new[] { "0000001", "settled", "maybe" }, "navarone: settled takes the user's keep or replace, not \"maybe\"")]
    [InlineData(new[] { "0000001", "settled" }, "usage: navarone [--full-coverage]")]
    public async Task LargeOrSettledWithNoTextNoProblemOrNoIdAtTheCodeIsAUsageError(string[] args, string message)
    {
        Codebase(["0000001"], ("First", 11, "first"), ("Second", 11, "second"));
        await RunAsync("--full-coverage");
        var logged = events.Handler.Read().Count(each => each.Event is not RaisedEvent);

        Assert.Equal(ExitCodes.UsageError, await RunAsync(args));

        Assert.Equal(message, error.ToString().Split(Environment.NewLine)[0]);
        Assert.Equal(logged, events.Handler.Read().Count(each => each.Event is not RaisedEvent));
    }

    [Fact]
    public async Task LargeAndSettledWriteTheirCost()
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("--full-coverage");
        output.GetStringBuilder().Clear();

        await RunAsync("0000001", "large", Text);

        Assert.Equal(new CostEvent("large", output.ToString().Length / 4), events.Handler.Read()[^1].Event);
        output.GetStringBuilder().Clear();

        await RunAsync("0000001", "settled", "keep");

        Assert.Equal(new CostEvent("settled", output.ToString().Length / 4), events.Handler.Read()[^1].Event);
    }

    // The log without the cost of each command an agent read.
    private List<LoggedEvent> Logged() => [.. events.Handler.Read().Where(logged => logged.Event is not CostEvent)];

    // What `navarone disputes --json` prints, run from the repo.
    private async Task<string> DisputesAsync()
    {
        using var listed = new StringWriter();
        Assert.Equal(ExitCodes.Listed, await Commands.RunAsync(["disputes", "--json"], repo.Root, events.Handler, Wire, listed, error));
        return listed.ToString().TrimEnd();
    }

    // A solution at the repo root whose type T holds the given methods.
    private void Codebase(params (string Symbol, int Branches, string Code)[] methods) => Codebase([], methods);

    // A solution at the repo root whose type T holds the given methods, with the given ids written at First.
    private void Codebase(string[] idsAtFirst, params (string Symbol, int Branches, string Code)[] methods)
    {
        var solution = Path.Combine(repo.Root, "a.sln");
        File.WriteAllText(solution, "");
        provider.Codebases[solution] = FakeFacts.WithIds(FakeFacts.With(methods), "First", idsAtFirst);
    }

    private EngineSetup Wire(string repoRoot, NavaroneEventHandler log) => events.Isolate(Wiring.For(repoRoot, log)) with { Provider = provider };

    private Task<int> RunAsync(params string[] args) => Commands.RunAsync(args, repo.Root, events.Handler, Wire, output, error);

    private Task<int> RunInAsync(string worktree, params string[] args) => Commands.RunAsync(args, worktree, events.Handler, Wire, output, error);
}
