using System.Text.Json.Nodes;
using Navarone.Cli;
using Navarone.Engine;
using Navarone.Engine.Configuration;
using Navarone.Engine.Events;
using Navarone.Engine.Facts;
using Navarone.Engine.Reasons;
using Navarone.Tests.Support;

namespace Navarone.Tests.Cli;

/// <summary>
/// What `navarone` writes to its event log: a run with what it showed and cost, a raised, a fixed with the code before
/// and after, a resolved, and the validator's keep and replace. Each test logs to a temporary data folder.
/// </summary>
public sealed class EventLogTests : IDisposable
{
    private const string Sound = "a flat dispatch over the eleven token kinds the lexer emits";

    private readonly TempRepo repo = new();
    private readonly TempEvents events = new();
    private readonly TempTaste taste = new();
    private readonly FakeProvider provider = new();
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public EventLogTests() => Directory.CreateDirectory(Path.Combine(repo.Root, ".git"));

    public void Dispose()
    {
        output.Dispose();
        error.Dispose();
        repo.Dispose();
        events.Dispose();
        taste.Dispose();
    }

    [Fact]
    public async Task ARunLogsTheIdsItShowedWithTheCodeOfEachRedOneAndItsCostInTokens()
    {
        Codebase(("First", 11, "first"), ("Fine", 2, "fine"));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync("--full-coverage"));

        var logged = EventsOtherThanCost();
        Assert.Equal(["raised", "run"], logged.Select(Type));
        Assert.Equal(new RaisedEvent("0000001", "cyclomatic", "First"), logged[0].Event);
        var run = Assert.IsType<RunEvent>(logged[1].Event);
        var shown = Assert.Single(run.Shown);
        Assert.Equal(new ShownProblem("0000001", "cyclomatic", "First", FindingState.Red, NavaroneEventHandler.HashOf("first")), shown);
        Assert.Equal(output.ToString().Length / 4, run.Tokens);
        Assert.True(run.Tokens > 0);
        Assert.Equal((repo.Root, repo.Root, null, null), (logged[1].Place.Repo, logged[1].Place.Worktree, logged[1].Place.Branch, logged[1].Place.Head));
        Assert.Equal("first", events.Handler.Code(shown.Code!));
    }

    [Fact]
    public async Task AProblemThatWasRedAndIsGoneFromTheCodeIsFixedWithTheCodeBeforeAndAfter()
    {
        Codebase(("First", 11, "first"));
        await RunAsync("--full-coverage");
        Codebase(("First", 2, "first fixed"));

        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
        await RunAsync("--full-coverage");

        var fix = Assert.Single(EventsOtherThanCost().Select(logged => logged.Event).OfType<FixedEvent>());
        Assert.Equal(new FixedEvent("0000001", "cyclomatic", "First", NavaroneEventHandler.HashOf("first"), NavaroneEventHandler.HashOf("first fixed"), false), fix);
        Assert.Equal(Position.Replace, fix.Builder);
        Assert.Equal(["first", "first fixed"], [events.Handler.Code(fix.Before), events.Handler.Code(fix.After!)]);
    }

    [Fact]
    public async Task AFixWhoseSubjectIsGoneHasNoCodeAfter()
    {
        Codebase(("First", 11, "first"), ("Fine", 2, "fine"));
        await RunAsync("--full-coverage");
        Codebase(("Fine", 2, "fine"));

        await RunAsync("--full-coverage");

        var fix = Assert.Single(EventsOtherThanCost().Select(logged => logged.Event).OfType<FixedEvent>());
        Assert.Null(fix.After);
    }

    [Fact]
    public async Task AReasonIsLoggedAndAResolvedProblemWhoseCodeChangesIsRaisedAgain()
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("--full-coverage");
        Assert.Equal(ExitCodes.Resolved, await RunAsync("0000001", Sound));
        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
        Codebase(["0000001"], ("First", 11, "first changed"));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync("--full-coverage"));

        var logged = EventsOtherThanCost();
        Assert.Equal(["raised", "run", "resolved", "run", "raised", "run"], logged.Select(Type));
        Assert.Equal(new ResolvedEvent("0000001", Sound, new DefendedCode(NavaroneEventHandler.HashOf("first"), "T.cs", 1, 1, "method T.First class:short callers:3")), logged[2].Event);
        Assert.Equal(new RaisedEvent("0000001", "cyclomatic", "First"), logged[4].Event);
    }

    [Fact]
    public async Task AResolvedProblemThisWorktreeNeverShowedIsRaisedOnceItsCodeChanges()
    {
        using var master = new TempEvents();
        Codebase(["0000001"], ("First", 11, "first"));
        await Commands.RunAsync(["0000001", Sound], repo.Root, master.Handler, Wire, output, error);
        Codebase(["0000001"], ("First", 11, "first changed"));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync("--full-coverage"));
        await RunAsync("--full-coverage");

        var logged = EventsOtherThanCost();
        Assert.Equal(["raised", "run", "run"], logged.Select(Type));
        Assert.Equal(new RaisedEvent("0000001", "cyclomatic", "First"), logged[0].Event);
    }

    [Fact]
    public async Task AFixAfterARefusedReasonIsASecondRoundThoughNoRunShowedTheProblemRed()
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("0000001", Sound);
        await RunAsync("0000001", "replace", "2", "the dispatch hides two jobs in one switch");
        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
        Codebase(["0000001"], ("First", 2, "first rewritten"));

        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "keep"));

        var logged = EventsOtherThanCost();
        Assert.Equal(["raised", "resolved", "refused-reason", "run", "fixed", "run", "accepted"], logged.Select(Type));
        Assert.Equal(new FixedEvent("0000001", "cyclomatic", "First", NavaroneEventHandler.HashOf("first"), NavaroneEventHandler.HashOf("first rewritten"), true), logged[4].Event);
        Assert.Equal(new AcceptedEvent("0000001", Position.Replace), logged[6].Event);
    }

    [Fact]
    public async Task AFixAfterARefusedFixIsASecondRoundOnceTheCodeChangesAgain()
    {
        Codebase(("First", 11, "first"));
        await RunAsync("--full-coverage");
        Codebase(("First", 2, "first split"));
        await RunAsync("--full-coverage");
        await RunAsync("0000001", "replace", "2", "the split only moved the branches into helpers");
        await RunAsync("--full-coverage");
        Codebase(("First", 2, "first rewritten"));

        await RunAsync("--full-coverage");

        var logged = EventsOtherThanCost();
        Assert.Equal(["raised", "run", "fixed", "run", "refused-fix", "run", "fixed", "run"], logged.Select(Type));
        Assert.Equal(new FixedEvent("0000001", "cyclomatic", "First", NavaroneEventHandler.HashOf("first split"), NavaroneEventHandler.HashOf("first rewritten"), true), logged[6].Event);
    }

    [Fact]
    public async Task KeepOnAResolvedProblemLogsThatTheValidatorAcceptsTheReasonAndChangesNothingElse()
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("0000001", Sound);
        var reasons = await ReasonsAsync();
        output.GetStringBuilder().Clear();

        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "keep"));

        Assert.Equal("0000001 keep: the validator accepts the reason", output.ToString().TrimEnd());
        var accepted = Assert.IsType<AcceptedEvent>(EventsOtherThanCost()[^1].Event);
        Assert.Equal((Position.Keep, Position.Keep), (accepted.Builder, accepted.Validator));
        Assert.Equal(reasons, await ReasonsAsync());
    }

    [Theory]
    [InlineData("keep")]
    [InlineData("reason")]
    [InlineData("large")]
    public async Task ACommandWhoseEventCannotBeWrittenExitsWithAUsageErrorSaysSoOnOneLineAndStoresNothing(string command)
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("--full-coverage");
        if (command == "keep")
        {
            await RunAsync("0000001", Sound);
        }

        var reasons = await ReasonsAsync();
        output.GetStringBuilder().Clear();
        var lockPath = Path.Combine(events.Folder, "events", ".lock");
        File.Delete(lockPath);
        Directory.CreateDirectory(lockPath);
        string[] args = command switch
        {
            "keep" => ["0000001", "keep"],
            "reason" => ["0000001", Sound],
            _ => ["0000001", "large", "the lexer and the parser share one table"],
        };

        Assert.Equal(ExitCodes.UsageError, await RunAsync(args));

        Assert.Equal("", output.ToString());
        var line = Assert.Single(error.ToString().TrimEnd().Split('\n'));
        Assert.StartsWith("navarone: the event log was not written, 1 event(s) lost: ", line, StringComparison.Ordinal);
        Assert.Equal(reasons, await ReasonsAsync());
    }

    [Fact]
    public async Task KeepOnAFixedProblemWhoseFindingIsGoneLogsThatTheValidatorAcceptsTheFix()
    {
        Codebase(("First", 11, "first"));
        await RunAsync("--full-coverage");
        Codebase(("First", 2, "first fixed"));
        await RunAsync("--full-coverage");
        output.GetStringBuilder().Clear();

        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "keep"));

        Assert.Equal("0000001 keep: the validator accepts the fix", output.ToString().TrimEnd());
        Assert.Equal(new AcceptedEvent("0000001", Position.Replace), EventsOtherThanCost()[^1].Event);
    }

    [Fact]
    public async Task ReplaceOnAFixedProblemLogsTheFixItRefusesAndTheNextFixIsASecondRound()
    {
        Codebase(("First", 11, "first"));
        await RunAsync("--full-coverage");
        Codebase(("First", 2, "first split"));
        await RunAsync("--full-coverage");
        var reasons = await ReasonsAsync();

        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "replace", "2", "the split only moved the branches into helpers"));
        Assert.Equal(reasons, await ReasonsAsync());
        Codebase(("First", 11, "first"));
        await RunAsync("--full-coverage");
        Codebase(("First", 2, "first rewritten"));
        await RunAsync("--full-coverage");

        var logged = EventsOtherThanCost();
        Assert.Equal(["raised", "run", "fixed", "run", "refused-fix", "run", "fixed", "run"], logged.Select(Type));
        var refusal = Assert.IsType<RefusedFixEvent>(logged[4].Event);
        Assert.Equal(new RefusedFixEvent("0000001", "cyclomatic", "First", "the split only moved the branches into helpers", NavaroneEventHandler.HashOf("first"), NavaroneEventHandler.HashOf("first split"), new TasteRule(2, TempTaste.Second)), refusal);
        Assert.Equal((Position.Replace, Position.Replace), (refusal.Builder, refusal.Validator));
        Assert.True(Assert.IsType<FixedEvent>(logged[6].Event).SecondRound);
        Assert.False(Assert.IsType<FixedEvent>(logged[2].Event).SecondRound);
    }

    [Fact]
    public async Task ReplaceOnAResolvedProblemLogsTheCodeTheReasonDefends()
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("0000001", Sound);
        var reasons = await ReasonsAsync();
        output.GetStringBuilder().Clear();

        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "replace", "2", "the dispatch hides two jobs in one switch"));

        Assert.Equal("0000001 replace: the validator refuses the reason; the code must still change", output.ToString().TrimEnd());
        var refusal = Assert.IsType<RefusedReasonEvent>(EventsOtherThanCost()[^1].Event);
        Assert.Equal(new RefusedReasonEvent("0000001", "cyclomatic", "First", "the dispatch hides two jobs in one switch", NavaroneEventHandler.HashOf("first"), new TasteRule(2, TempTaste.Second)), refusal);
        Assert.Equal("first", events.Handler.Code(refusal.Before));
        Assert.Equal(reasons, await ReasonsAsync());
    }

    [Fact]
    public async Task ReplaceNamesARuleOfTheReposOwnNavaroneMdBeforeTheGlobalOne()
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("0000001", Sound);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "NAVARONE.md"), "## The rules\n\n1. Plain.\n4. The repo's own fourth rule.\n", TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "replace", "4", "the dispatch hides two jobs in one switch"));

        var refusal = Assert.IsType<RefusedReasonEvent>(EventsOtherThanCost()[^1].Event);
        Assert.Equal(new TasteRule(4, "The repo's own fourth rule."), refusal.Rule);
    }

    [Fact]
    public async Task ReplaceReadsTheGlobalRulesWhenTheReposNavaroneMdNumbersNone()
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("0000001", Sound);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "NAVARONE.md"), "# This repo\n\n## The process\n\n1. Not a rule.\n", TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "replace", "2", "the dispatch hides two jobs in one switch"));

        var refusal = Assert.IsType<RefusedReasonEvent>(EventsOtherThanCost()[^1].Event);
        Assert.Equal(new TasteRule(2, TempTaste.Second), refusal.Rule);
    }

    [Theory]
    [InlineData(new[] { "0000001", "keep" }, "navarone: 0000001 is still red with no reason: there is nothing to accept or refuse")]
    [InlineData(new[] { "0000001", "replace", "2", "it must change" }, "navarone: 0000001 is still red with no reason: there is nothing to accept or refuse")]
    [InlineData(new[] { "0000009", "keep" }, "navarone: Navarone never granted the id 0000009")]
    [InlineData(new[] { "0000001", "replace", "2", " " }, "navarone: replace takes the finding: why the code must still change")]
    [InlineData(new[] { "0000001", "replace", "it must change" }, "navarone: replace names the rule of NAVARONE.md the code breaks, by its number there: navarone <id> replace <rule> \"<finding>\"")]
    [InlineData(new[] { "0000001", "replace", "4", "it must change" }, "navarone: replace names the rule the code breaks by its number in NAVARONE.md, 1 to 3, not \"4\": navarone <id> replace <rule> \"<finding>\"")]
    [InlineData(new[] { "0000001", "replace", "rule 2", "it must change" }, "navarone: replace names the rule the code breaks by its number in NAVARONE.md, 1 to 3, not \"rule 2\": navarone <id> replace <rule> \"<finding>\"")]
    [InlineData(new[] { "0000001", "replace" }, "usage: navarone [--full-coverage]")]
    public async Task KeepOrReplaceWithNothingToAcceptOrRefuseIsAUsageErrorAndKeepIsNoReason(string[] args, string message)
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("--full-coverage");
        var logged = events.Handler.Read().Count;

        Assert.Equal(ExitCodes.UsageError, await RunAsync(args));

        Assert.Equal(message, error.ToString().Split(Environment.NewLine)[0]);
        Assert.Null(ReasonsFile.Load(repo.Root).For(new ProblemId("0000001"))!.Resolution);
        Assert.Equal(logged, events.Handler.Read().Count);
    }

    // A resolved problem no run showed red and no refusal reopened is not fixed when its finding goes: Navarone did not
    // send the builder to it.
    [Fact]
    public async Task KeepOnAProblemWithNoFindingAndNoFixIsAUsageError()
    {
        Codebase(("First", 11, "first"), ("Fine", 2, "fine"));
        await RunAsync("0000001", Sound);
        Codebase(("First", 2, "first"), ("Fine", 2, "fine"));

        Assert.Equal(ExitCodes.UsageError, await RunAsync("0000001", "keep"));

        Assert.Equal("navarone: 0000001 has no finding in the code as it is now and no fix on this worktree: there is nothing to accept or refuse", error.ToString().TrimEnd());
    }

    [Fact]
    public async Task EventsJsonPrintsTheLogAsOneDocumentFromAnyDirectoryAndWritesNothing()
    {
        Codebase(("First", 11, "first"));
        await RunAsync("--full-coverage");
        var before = Snapshot();
        output.GetStringBuilder().Clear();
        var outside = Directory.CreateTempSubdirectory("navarone-outside-").FullName;
        try
        {
            Assert.Equal(ExitCodes.Printed, await Commands.RunAsync(["events", "--json"], outside, events.Handler, Wire, output, error));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }

        var document = JsonNode.Parse(output.ToString())!;
        Assert.Equal("navarone", (string?)document["source"]);
        Assert.Equal(["raised", "run"], document["events"]!.AsArray().Select(line => (string?)line!["type"]));
        Assert.Equal(before, Snapshot());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task CodePrintsTheCodeStoredUnderAHashAndAHashWithNoCodeIsAUsageError()
    {
        var hash = events.Handler.Store("void First() { }");

        Assert.Equal(ExitCodes.Printed, await RunAsync("code", hash));
        Assert.Equal(ExitCodes.UsageError, await RunAsync("code", "0123"));

        Assert.Equal("void First() { }", output.ToString());
        Assert.Equal("navarone: no code stored under 0123", error.ToString().TrimEnd());
    }

    [Fact]
    public async Task InAGitWorktreeAnEventNamesTheMainCheckoutTheWorktreeItsBranchAndHead()
    {
        Directory.Delete(Path.Combine(repo.Root, ".git"));
        repo.Git("init", "-q", "-b", "master");
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "a.sln"), "", TestContext.Current.CancellationToken);
        repo.Git("add", "a.sln");
        repo.Commit();
        var worktree = Path.Combine(Directory.CreateTempSubdirectory("navarone-worktree-").FullName, "feature");
        repo.Git("worktree", "add", "-q", worktree, "-b", "feature");
        provider.Codebases[Path.Combine(worktree, "a.sln")] = FakeFacts.With(("First", 11, "first"));
        try
        {
            Assert.Equal(ExitCodes.AnyRed, await Commands.RunAsync(["--full-coverage"], worktree, events.Handler, Wire, output, error));
        }
        finally
        {
            repo.Git("worktree", "remove", "--force", worktree);
            Directory.Delete(Path.GetDirectoryName(worktree)!, recursive: true);
        }

        var place = EventsOtherThanCost()[^1].Place;
        Assert.Equal(new EventPlace(repo.Root, worktree, "feature", repo.Git("rev-parse", "HEAD").Trim()), place);
    }

    [Fact]
    public async Task ACommandAnAgentReadsLogsOneCostWithItsNameAndTokensAndNoRun()
    {
        Codebase(("First", 11, "first"));

        Assert.Equal(ExitCodes.Printed, await RunAsync("lens", "method", "T.First"));

        var logged = Assert.Single(events.Handler.Read());
        Assert.Equal(new CostEvent("lens", output.ToString().Length / 4), logged.Event);
        Assert.True(((CostEvent)logged.Event).Tokens > 0);
    }

    [Theory]
    [InlineData("lens", "--json", "method", "T.First")]
    [InlineData("disputes", "--json")]
    [InlineData("events", "--json")]
    public async Task ACommandForBrainLogsNoCost(params string[] args)
    {
        Codebase(("First", 11, "first"));

        await RunAsync(args);

        Assert.Empty(events.Handler.Read());
    }

    [Fact]
    public async Task CodeOfAStoredHashLogsNoCost()
    {
        var hash = events.Handler.Store("void First() { }");

        Assert.Equal(ExitCodes.Printed, await RunAsync("code", hash));

        Assert.Empty(events.Handler.Read());
    }

    [Fact]
    public async Task EachCommandAnAgentReadsLogsOneCostWithItsOwnNameAndTheTokensItPrinted()
    {
        Codebase(("First", 11, "first"));
        await RunAsync("--full-coverage");
        Codebase(["0000001"], ("First", 11, "first"));
        var expected = new List<CostEvent>();
        foreach (var (name, args) in new[]
        {
            ("evidence", new[] { "evidence", "0000001", "--json" }),
            ("duplicates", new[] { "duplicates" }),
            ("init", new[] { "init" }),
            ("reason", new[] { "0000001", Sound }),
            ("", new[] { "--full-coverage" }),
            ("keep", new[] { "0000001", "keep" }),
            ("replace", new[] { "0000001", "replace", "2", "the dispatch hides two jobs in one switch" }),
        })
        {
            output.GetStringBuilder().Clear();
            await RunAsync(args);
            if (name.Length > 0)
            {
                expected.Add(new CostEvent(name, output.ToString().Length / 4));
            }
        }

        var costs = events.Handler.Read().Select(logged => logged.Event).OfType<CostEvent>().ToList();
        Assert.Equal(expected, costs);
        Assert.All(costs, cost => Assert.True(cost.Tokens > 0));
    }

    [Fact]
    public async Task ALogThatCannotStoreCodeCostsOneLineOnStderrAndNotTheExitCodeOrTheOutput()
    {
        Codebase(("First", 11, "first"));
        File.WriteAllText(Path.Combine(events.Folder, "code"), "a file where the code store's folder belongs");

        var exit = await Commands.RunAsync(["--full-coverage"], repo.Root, events.Reporting(error), Wire, output, error);

        Assert.Equal(ExitCodes.AnyRed, exit);
        Assert.Contains("0000001 cyclomatic", output.ToString(), StringComparison.Ordinal);
        var line = Assert.Single(error.ToString().TrimEnd().Split('\n'));
        Assert.StartsWith("navarone: the code under ", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACommandThatEndsInAUsageErrorLogsNoCost()
    {
        Codebase(("First", 11, "first"));

        Assert.Equal(ExitCodes.UsageError, await RunAsync("lens", "method", "T.Second"));

        Assert.Empty(events.Handler.Read());
    }

    [Fact]
    public async Task ALogThatCannotBeWrittenCostsTheLineOneLineOnStderrAndNotTheExitCode()
    {
        Codebase(("First", 11, "first"));
        Directory.CreateDirectory(Path.Combine(events.Folder, "events", ".lock"));

        var exit = await Commands.RunAsync(["--full-coverage"], repo.Root, events.Reporting(error), Wire, output, error);

        Assert.Equal(ExitCodes.AnyRed, exit);
        Assert.Contains("0000001 cyclomatic", output.ToString(), StringComparison.Ordinal);
        var line = Assert.Single(error.ToString().TrimEnd().Split('\n'));
        Assert.StartsWith("navarone: the event log was not written, 2 event(s) lost: ", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAppendAfterATornLineStartsALineOfItsOwnAndEveryCommandStillReadsTheLog()
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("--full-coverage");
        File.AppendAllText(TheMonthsLog(), """{"type":"raised","at":""");

        Assert.Equal(ExitCodes.Resolved, await RunAsync("0000001", Sound));

        Assert.Contains(EventsOtherThanCost(), logged => logged.Event is ResolvedEvent);
        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
    }

    [Fact]
    public async Task EventsJsonSkipsTornLinesLikeEveryOtherCommandAndNamesEachOnStderr()
    {
        Codebase(("First", 11, "first"));
        await RunAsync("--full-coverage");
        var firstTorn = File.ReadAllLines(TheMonthsLog()).Length + 1;
        File.AppendAllText(TheMonthsLog(), "{\"type\":\n");

        Assert.Equal(ExitCodes.AnyRed, await RunAsync("--full-coverage"));
        var secondTorn = File.ReadAllLines(TheMonthsLog()).Length + 1;
        File.AppendAllText(TheMonthsLog(), "{\"type\":\n");
        Assert.Equal(ExitCodes.Listed, await RunAsync("disputes", "--json"));
        Assert.Equal(string.Empty, error.ToString());
        output.GetStringBuilder().Clear();
        Assert.Equal(ExitCodes.Printed, await RunAsync("events", "--json"));

        var line = Assert.Single(error.ToString().TrimEnd().Split('\n'));
        Assert.Equal($"navarone: skipped torn lines: {TheMonthsLog()} line {firstTorn}, {TheMonthsLog()} line {secondTorn}", line);
        var document = JsonNode.Parse(output.ToString())!;
        Assert.Equal(File.ReadAllLines(TheMonthsLog()).Length - 2, document["events"]!.AsArray().Count);
    }

    [Fact]
    public async Task ANavaroneJsonThatCannotBeWrittenIsOneLineOnStderrAndExitTwo()
    {
        Directory.CreateDirectory(Path.Combine(repo.Root, NavaroneConfig.FileName));
        File.WriteAllText(Path.Combine(repo.Root, NavaroneConfig.FileName, "x"), "");

        Assert.Equal(ExitCodes.UsageError, await RunAsync("init"));

        var line = Assert.Single(error.ToString().TrimEnd().Split('\n'));
        Assert.Contains("was not written", line, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(repo.Root, $".{NavaroneConfig.FileName}.*"));
    }

    [Fact]
    public async Task AReasonsJsonThatCannotBeWrittenIsOneLineOnStderrAndExitTwo()
    {
        Codebase(("First", 11, "first"));
        Directory.CreateDirectory(Path.Combine(repo.Root, ReasonsFile.FileName));
        File.WriteAllText(Path.Combine(repo.Root, ReasonsFile.FileName, "x"), "");

        Assert.Equal(ExitCodes.UsageError, await RunAsync("--full-coverage"));

        var line = Assert.Single(error.ToString().TrimEnd().Split('\n'));
        Assert.Contains("was not written", line, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(repo.Root, $".{ReasonsFile.FileName}.*"));
    }

    // The one month file the log holds.
    private string TheMonthsLog() => Assert.Single(Directory.GetFiles(Path.Combine(events.Folder, "events"), "*.jsonl"));

    // The log without the cost of each command an agent read, which the tests of the other events leave out.
    private List<LoggedEvent> EventsOtherThanCost() => [.. events.Handler.Read().Where(logged => logged.Event is not CostEvent)];

    private static string Type(LoggedEvent logged) => (string)logged.Json()["type"]!;

    // A solution at the repo root whose type T holds the given methods.
    private void Codebase(params (string Symbol, int Branches, string Code)[] methods) => Codebase([], methods);

    // A solution at the repo root whose type T holds the given methods, with the given ids written at First.
    private void Codebase(string[] idsAtFirst, params (string Symbol, int Branches, string Code)[] methods)
    {
        var solution = Path.Combine(repo.Root, "a.sln");
        File.WriteAllText(solution, "");
        provider.Codebases[solution] = FakeFacts.WithIds(FakeFacts.With(methods), "First", idsAtFirst);
    }

    private Task<string> ReasonsAsync() => File.ReadAllTextAsync(Path.Combine(repo.Root, ReasonsFile.FileName), TestContext.Current.CancellationToken);

    // Every file in the data folder with its text.
    private List<(string, string)> Snapshot() =>
        [.. Directory.GetFiles(events.Folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(path => (path, File.ReadAllText(path)))];

    private EngineSetup Wire(string repoRoot, NavaroneEventHandler log) => events.Isolate(Wiring.For(repoRoot, log)) with { Provider = provider, Taste = taste.Path };

    private Task<int> RunAsync(params string[] args) => Commands.RunAsync(args, repo.Root, events.Handler, Wire, output, error);
}
