using System.Text.Json.Nodes;
using Navarone.Cli;
using Navarone.Engine;
using Navarone.Engine.Clashes;
using Navarone.Engine.Events;
using Navarone.Engine.Reasons;
using Navarone.Tests.Support;

namespace Navarone.Tests.Cli;

/// <summary>
/// A clash: the validator refused the builder's answer with `navarone &lt;id&gt; replace &lt;rule&gt;
/// "&lt;finding&gt;"`, and the builder held its position with a new reason for the code refused. The clash holds the
/// problem red, `navarone disputes --json` lists it as a bench for brain's window, and the next run collects the user's
/// judgement from brain's answers. Each test logs to a temporary data folder and reads answers from a temporary folder.
/// </summary>
public sealed class ClashTests : IDisposable
{
    private const string Sound = "a flat dispatch over the eleven token kinds the lexer emits";
    private const string Holds = "the eleven branches are the eleven token kinds, and one switch reads them best";
    private const string Finding = "The switch crams the parse and the check into one method. Parse first, then check, in two steps.";

    private readonly TempRepo repo = new();
    private readonly TempEvents events = new();
    private readonly TempTaste taste = new();
    private readonly string answers = Directory.CreateTempSubdirectory("navarone-answers-").FullName;
    private readonly FakeProvider provider = new();
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public ClashTests() => Directory.CreateDirectory(Path.Combine(repo.Root, ".git"));

    public void Dispose()
    {
        output.Dispose();
        error.Dispose();
        repo.Dispose();
        events.Dispose();
        taste.Dispose();
        Directory.Delete(answers, recursive: true);
    }

    [Fact]
    public async Task ANewReasonForTheRefusedCodeIsAClashThatHoldsTheProblemRed()
    {
        await ClashOnAReasonAsync();
        output.GetStringBuilder().Clear();

        Assert.Equal(ExitCodes.AnyRed, await RunAsync("--full-coverage"));

        Assert.Equal("0000001 cyclomatic First at T.cs:1: the builder and the validator clash, red until the user judges it in brain's window: The switch crams the parse and the check into one method.", output.ToString().TrimEnd());
        var run = Assert.IsType<RunEvent>(Logged()[^1].Event);
        Assert.Equal(new ShownProblem("0000001", "cyclomatic", "First", FindingState.Red, NavaroneEventHandler.HashOf("first")), Assert.Single(run.Shown));
    }

    [Fact]
    public async Task TheMapShowsAClashHeldProblemAsAClashWithItsMethodAndLogsNothing()
    {
        await ClashOnAReasonAsync();
        var logged = events.Handler.Read().Count;
        output.GetStringBuilder().Clear();

        Assert.Equal(ExitCodes.Printed, await RunAsync("map", "--json"));

        var problem = Assert.Single(JsonNode.Parse(output.ToString())!["problems"]!.AsArray())!;
        Assert.Equal(("0000001", "T.First", "clash", "cyclomatic"), ((string?)problem["id"], (string?)problem["method"], (string?)problem["state"], (string?)problem["check"]));
        Assert.StartsWith("the builder and the validator clash", (string?)problem["question"], StringComparison.Ordinal);
        Assert.Equal(logged, events.Handler.Read().Count);
    }

    [Fact]
    public async Task AClashHoldsTheProblemRedOutsideTheChangeToo()
    {
        Directory.Delete(Path.Combine(repo.Root, ".git"));
        repo.Git("init", "-q", "-b", "master");
        Codebase(["0000001"], ("First", 11, "first"));
        repo.Git("add", "a.sln");
        repo.Commit();
        await RunAsync("0000001", Sound);
        await RunAsync("0000001", "replace", "2", Finding);
        await RunAsync("0000001", Holds);
        output.GetStringBuilder().Clear();

        Assert.Equal(ExitCodes.AnyRed, await RunAsync());

        Assert.StartsWith("0000001 cyclomatic First at T.cs:1: the builder and the validator clash", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusalTheBuilderAnswersByChangingTheCodeIsNoClash()
    {
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAsync("0000001", Sound);
        await RunAsync("0000001", "replace", "2", Finding);
        Codebase(["0000001"], ("First", 11, "first changed"));

        Assert.Equal(ExitCodes.Resolved, await RunAsync("0000001", Holds));

        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
        Assert.Empty(await QuestionsAsync());
    }

    [Fact]
    public async Task TheValidatorsKeepEndsTheClash()
    {
        await ClashOnAReasonAsync();

        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "keep"));

        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
        Assert.Empty(await QuestionsAsync());
    }

    [Fact]
    public async Task DisputesListsAClashOnAReasonAsABenchFromAnyDirectory()
    {
        await ClashOnAReasonAsync();
        await RunAsync("--full-coverage");

        var question = Assert.Single(await QuestionsAsync());

        var clashedAt = Logged().Last(logged => logged.Event is ResolvedEvent).At;
        Assert.Matches("^0000001-[0-9a-f]{16}$", (string?)question["id"]);
        Assert.Equal(repo.Root, (string?)question["repo"]);
        Assert.Equal(TempTaste.Second, (string?)question["title"]);
        Assert.Equal(repo.Root, (string?)question["group"]);
        Assert.Equal(["cyclomatic"], question["tags"]!.AsArray().Select(tag => (string?)tag));
        Assert.Equal(clashedAt, question["created"]!.GetValue<DateTimeOffset>());
        Assert.False((bool)question["stale"]!);
        Assert.Equal("method T.First class:short callers:3", (string?)question["context"]);
        Assert.Equal("""[{"path":"T.cs","lines":[1]}]""", question["focus"]!.ToJsonString());
        Assert.Equal(["keep", "replace"], question["choices"]!.AsArray().Select(choice => (string?)choice!["id"]));
        var blocks = question["blocks"]!.AsArray();
        Assert.Equal(["text", "code", "text", "text", "facts"], blocks.Select(block => (string?)block!["type"]));
        Assert.Equal("The switch crams the parse and the check into one method.", (string?)blocks[0]!["text"]);
        Assert.Equal(("T.cs", 1, "first"), ((string?)blocks[1]!["path"], (int)blocks[1]!["startLine"]!, (string?)blocks[1]!["code"]));
        Assert.Equal(("The builder", Holds), ((string?)blocks[2]!["caption"], (string?)blocks[2]!["text"]));
        Assert.Equal(("The validator", Finding), ((string?)blocks[3]!["caption"], (string?)blocks[3]!["text"]));
        var details = blocks[4]!["facts"]!.AsArray().Select(fact => ((string?)fact!["label"], (string?)fact["value"])).ToList();
        Assert.Contains(("Problem", "0000001"), details);
        Assert.Contains(("Rule", "2"), details);
        Assert.Contains(("Code", NavaroneEventHandler.HashOf("first")), details);
    }

    [Fact]
    public async Task AClashOnARefusedFixShowsTheCodeBeforeAndTheFixAsADiff()
    {
        Codebase(("First", 11, "first"));
        await RunAsync("--full-coverage");
        Codebase(("First", 2, "first split"));
        await RunAsync("--full-coverage");
        await RunAsync("0000001", "replace", "2", Finding);

        Assert.Equal(ExitCodes.Resolved, await RunAsync("0000001", Holds));

        Assert.Equal(new ResolvedEvent("0000001", Holds, new DefendedCode(NavaroneEventHandler.HashOf("first split"), "T.cs", 1, 1, "method T.First class:short callers:3")), Logged()[^1].Event);
        Assert.Null(ReasonsFile.Load(repo.Root).For(new ProblemId("0000001"))!.Resolution);
        Assert.Equal(ExitCodes.AnyRed, await RunAsync("--full-coverage"));
        var diff = (await QuestionsAsync()).Single()["blocks"]![1]!;
        Assert.Equal("diff", (string?)diff["type"]);
        Assert.Equal("""[{"path":"T.cs","before":"first","after":"first split"}]""", diff["files"]!.ToJsonString());
    }

    [Fact]
    public async Task AReasonForAFixedProblemWithNoRefusedFixIsAUsageError()
    {
        Codebase(("First", 11, "first"));
        await RunAsync("--full-coverage");
        Codebase(("First", 2, "first split"));
        await RunAsync("--full-coverage");

        Assert.Equal(ExitCodes.UsageError, await RunAsync("0000001", Holds));

        Assert.Equal("navarone: no problem with id 0000001 in the code as it is now, and no refused fix of it on this worktree", error.ToString().TrimEnd());
    }

    [Fact]
    public async Task TheUsersKeepMakesTheBuildersReasonTheResolution()
    {
        await ClashOnAReasonAsync();
        Answer(await OnlyQuestionIdAsync(), "keep", "the token kinds are the domain");

        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));

        Assert.Contains(new SettledEvent("0000001", "cyclomatic", "First", Position.Keep, "the token kinds are the domain", NavaroneEventHandler.HashOf("first")), Logged().Select(each => each.Event));
        Assert.Equal(Holds, ReasonsFile.Load(repo.Root).For(new ProblemId("0000001"))!.Resolution!.Reason);
        Assert.Empty(await QuestionsAsync());
        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
        Assert.Single(Logged(), each => each.Event is SettledEvent);
    }

    [Fact]
    public async Task TheUsersReplaceKeepsTheProblemRedWithTheValidatorsFindingAndHisWordsUntilTheCodeChanges()
    {
        await ClashOnAReasonAsync();
        Answer(await OnlyQuestionIdAsync(), "replace", "name the two steps");
        output.GetStringBuilder().Clear();

        Assert.Equal(ExitCodes.AnyRed, await RunAsync("--full-coverage"));

        Assert.Equal($"0000001 cyclomatic First at T.cs:1: the user sided with the validator in brain's window, so the code must change: {Finding} The user adds: name the two steps", output.ToString().TrimEnd());
        Assert.Contains(new SettledEvent("0000001", "cyclomatic", "First", Position.Replace, "name the two steps", NavaroneEventHandler.HashOf("first")), Logged().Select(each => each.Event));
        Assert.Empty(await QuestionsAsync());
        Codebase(("First", 2, "first in two steps"));

        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));

        var fix = Assert.Single(Logged().Select(each => each.Event).OfType<FixedEvent>());
        Assert.Equal(new FixedEvent("0000001", "cyclomatic", "First", NavaroneEventHandler.HashOf("first"), NavaroneEventHandler.HashOf("first in two steps"), true), fix);
    }

    [Fact]
    public async Task AnAnswerAboutCodeThatChangedSinceIsDroppedAndTheQuestionIsListedAgainAsStale()
    {
        await ClashOnAReasonAsync();
        var asked = await OnlyQuestionIdAsync();
        Answer(asked, "keep", null);
        Codebase(["0000001"], ("First", 11, "first changed"));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync("--full-coverage"));

        Assert.DoesNotContain(Logged(), each => each.Event is SettledEvent);
        var question = Assert.Single(await QuestionsAsync());
        Assert.NotEqual(asked, (string?)question["id"]);
        Assert.True((bool)question["stale"]!);
        Answer((string)question["id"]!, "keep", null);

        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
    }

    [Fact]
    public async Task TwoClashesOnTheSameProblemAndTheSameCodeAtDifferentTimesAreTwoQuestionsAndTheFirstAnswerDoesNotSettleTheSecond()
    {
        // Every command runs on a fixed clock in time order, so the log reads the same in any month.
        var first = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var second = first.AddDays(2);
        var later = second.AddHours(1);
        Codebase(["0000001"], ("First", 11, "first"));
        await RunAtAsync(first, "0000001", Sound);
        await RunAtAsync(first, "0000001", "replace", "2", Finding);
        await RunAtAsync(first, "0000001", Holds);
        var asked = await OnlyQuestionIdAsync();

        // The same problem, the same code, refused and defended again two days later.
        await RunAtAsync(second, "0000001", "replace", "2", Finding);
        await RunAtAsync(second, "0000001", "The eleven branches still are the eleven token kinds, and one switch reads them best");
        var askedAgain = await OnlyQuestionIdAsync();
        Assert.NotEqual(asked, askedAgain);

        // The user's keep to the first question would turn the run green if it settled the second clash.
        Answer(asked, "keep", null);
        Assert.Equal(ExitCodes.AnyRed, await RunAtAsync(later, "--full-coverage"));
        Assert.DoesNotContain(Logged(), each => each.Event is SettledEvent);
        Assert.Equal(askedAgain, await OnlyQuestionIdAsync());

        Answer(askedAgain, "keep", null);
        Assert.Equal(ExitCodes.AllGreen, await RunAtAsync(later, "--full-coverage"));
        Assert.Single(Logged(), each => each.Event is SettledEvent);
    }

    [Fact]
    public async Task AnAnswerWhoseChoiceIsNeitherKeepNorReplaceIsNoAnswer()
    {
        await ClashOnAReasonAsync();
        Answer(await OnlyQuestionIdAsync(), "maybe", null);

        Assert.Equal(ExitCodes.AnyRed, await RunAsync("--full-coverage"));

        Assert.DoesNotContain(Logged(), each => each.Event is SettledEvent);
    }

    [Fact]
    public async Task AnAgentCannotSettleAClashTheUserJudgesInBrainsWindow()
    {
        await ClashOnAReasonAsync();
        await RunAsync("0000001", "large", "The switch holds two jobs. It shows in First. Split the type in two.");

        Assert.Equal(ExitCodes.UsageError, await RunAsync("0000001", "settled", "keep"));

        Assert.Equal("navarone: 0000001 is in a clash: the user judges it in brain's window, and the next `navarone` collects his answer", error.ToString().TrimEnd());
        Assert.Equal(ExitCodes.AnyRed, await RunAsync("--full-coverage"));
    }

    [Fact]
    public async Task DisputesListsNoClashForAWorktreeThatIsGoneOrARefusalThatNamesNoRule()
    {
        var code = NavaroneEventHandler.HashOf("first");
        var defended = new DefendedCode(code, "T.cs", 1, 1, null);
        var gone = new EventPlace("/no/such/repo", "/no/such/repo", null, null);
        events.Handler.Write(gone, [new RefusedReasonEvent("0000001", "cyclomatic", "First", Finding, code, new TasteRule(2, TempTaste.Second)), new ResolvedEvent("0000001", Holds, defended)]);
        var here = new EventPlace(repo.Root, repo.Root, null, null);
        events.Handler.Write(here, [new RefusedReasonEvent("0000001", "cyclomatic", "First", Finding, code, null), new ResolvedEvent("0000001", Holds, defended)]);

        Assert.Empty(await QuestionsAsync());
    }

    // The builder resolves First with a reason, the validator refuses it for the second rule, and the builder gives a
    // new reason for the same code.
    private async Task ClashOnAReasonAsync()
    {
        Codebase(["0000001"], ("First", 11, "first"));
        Assert.Equal(ExitCodes.Resolved, await RunAsync("0000001", Sound));
        Assert.Equal(ExitCodes.Recorded, await RunAsync("0000001", "replace", "2", Finding));
        Assert.Equal(ExitCodes.Resolved, await RunAsync("0000001", Holds));
    }

    // The log without the cost of each command an agent read.
    private List<LoggedEvent> Logged() => [.. events.Handler.Read().Where(logged => logged.Event is not CostEvent)];

    // brain's window stores the user's answer to the question with the given id.
    private void Answer(string questionId, string choice, string? words)
    {
        var answer = new JsonObject { ["id"] = questionId, ["choice"] = choice, ["words"] = words, ["answeredAt"] = DateTimeOffset.UtcNow };
        File.WriteAllText(Path.Combine(answers, questionId + ".json"), answer.ToJsonString());
    }

    private async Task<string> OnlyQuestionIdAsync() => (string)Assert.Single(await QuestionsAsync())["id"]!;

    // The questions `navarone disputes --json` lists, run from a directory outside every repo.
    private async Task<List<JsonNode>> QuestionsAsync()
    {
        using var listed = new StringWriter();
        var outside = Directory.CreateTempSubdirectory("navarone-outside-").FullName;
        try
        {
            Assert.Equal(ExitCodes.Listed, await Commands.RunAsync(["disputes", "--json"], outside, events.Handler, Wire, listed, error));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }

        var document = JsonNode.Parse(listed.ToString())!;
        Assert.Equal("navarone", (string?)document["source"]);
        return [.. document["questions"]!.AsArray().Select(question => question!)];
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

    private EngineSetup Wire(string repoRoot, NavaroneEventHandler log) => Wiring.For(repoRoot, log) with { Provider = provider, Answers = new BrainAnswers(answers), Taste = taste.Path };

    // The command run at the given time, as the event log stamps it.
    private Task<int> RunAtAsync(DateTimeOffset at, params string[] args) => Commands.RunAsync(args, repo.Root, events.At(at), Wire, output, error);

    private Task<int> RunAsync(params string[] args) => Commands.RunAsync(args, repo.Root, events.Handler, Wire, output, error);
}
