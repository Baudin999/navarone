using Navarone.Cli;
using Navarone.Engine;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Reasons;
using Navarone.Tests.Support;

namespace Navarone.Tests.Engine;

public sealed class AnalysisTests : IDisposable
{
    private readonly TempRepo repo = new();
    private readonly TempEvents events = new();
    private readonly FakeProvider provider = new();

    public void Dispose()
    {
        repo.Dispose();
        events.Dispose();
    }

    [Fact]
    public async Task GrantsZeroPaddedIdsInOrderAndRecordsEachProblem()
    {
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 11, "first"), ("Fine", 2, "fine"), ("Second", 12, "second"));

        var verdicts = await RunAsync("a.sln");

        Assert.Equal(["0000001", "0000002"], verdicts.Select(verdict => verdict.Problem.Id.Value));
        Assert.All(verdicts, verdict => Assert.Equal(FindingState.Red, verdict.State));
        var file = ReasonsFile.Load(repo.Root);
        Assert.Equal(2, file.Granted);
        Assert.Equal(["First", "Second"], file.Problems.Select(problem => problem.Symbol.Value));
        Assert.All(file.Problems, problem => Assert.Equal("cyclomatic", problem.Check));
        Assert.Equal(CodeFingerprint.Of(new("First"), "first"), file.For(new ProblemId("0000001"))!.Fingerprint);
    }

    [Fact]
    public async Task AMethodOverBothLimitsIsTwoProblemsWithTwoIdsThatSurviveTheNextRun()
    {
        provider.Codebases["a.sln"] = FakeFacts.With(("Big", 16, "big"));

        var first = await RunAsync("a.sln");
        var second = await RunAsync("a.sln");

        Assert.Equal([("cyclomatic", "0000001"), ("cognitive", "0000002")], first.Select(verdict => (verdict.Problem.Check, verdict.Problem.Id.Value)));
        Assert.Equal(first.Select(verdict => verdict.Problem.Id), second.Select(verdict => verdict.Problem.Id));
        var file = ReasonsFile.Load(repo.Root);
        Assert.Equal(2, file.Granted);
        Assert.All(file.Problems, problem => Assert.Equal("Big", problem.Symbol.Value));
        Assert.Equal(["cyclomatic", "cognitive"], file.Problems.Select(problem => problem.Check));
    }

    [Fact]
    public async Task KeepsEachProblemsIdOnTheNextRunAndGrantsTheNextIdToANewOne()
    {
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 11, "first"), ("Second", 12, "second"));
        await RunAsync("a.sln");
        provider.Codebases["a.sln"] = FakeFacts.With(("New", 13, "new"), ("Second", 12, "second changed"), ("First", 11, "first"));

        var verdicts = await RunAsync("a.sln");

        Assert.Equal(
            [("New", "0000003"), ("Second", "0000002"), ("First", "0000001")],
            verdicts.Select(verdict => (verdict.Problem.Symbol.Value, verdict.Problem.Id.Value)));
        Assert.Equal(CodeFingerprint.Of(new("Second"), "second changed"), ReasonsFile.Load(repo.Root).For(new ProblemId("0000002"))!.Fingerprint);
    }

    [Fact]
    public async Task RemovesAProblemWhoseCodeIsGoneAndNeverGrantsItsIdAgain()
    {
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 11, "first"), ("Second", 12, "second"));
        await RunAsync("a.sln");
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 11, "first"));
        await RunAsync("a.sln");
        Assert.Equal(["0000001"], ReasonsFile.Load(repo.Root).Problems.Select(problem => problem.Id.Value));

        provider.Codebases["a.sln"] = FakeFacts.With(("First", 11, "first"), ("Third", 11, "third"));
        var verdicts = await RunAsync("a.sln");

        Assert.Equal("0000003", verdicts.Single(verdict => verdict.Problem.Symbol.Value == "Third").Problem.Id.Value);
    }

    [Fact]
    public async Task AFixedProblemStaysInReasonsJsonWhileItsCodeExistsAndIsNoLongerReported()
    {
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 11, "first"));
        await RunAsync("a.sln");
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 2, "first fixed"));

        var verdicts = await RunAsync("a.sln");

        Assert.DoesNotContain(verdicts, verdict => verdict.Problem.Symbol.Value == "First");
        Assert.Equal(["0000001"], ReasonsFile.Load(repo.Root).Problems.Select(problem => problem.Id.Value));
    }

    [Fact]
    public async Task ReportsASubjectTwoCodebasesShareOnceWithOneId()
    {
        var shared = FakeFacts.With("SharedType", ("Shared", 11, "shared")).Types;
        provider.Codebases["a.sln"] = new([.. shared, .. FakeFacts.With("A", ("OnlyA", 11, "a")).Types], Complete: true);
        provider.Codebases["b.slnx"] = new([.. shared, .. FakeFacts.With("B", ("OnlyB", 11, "b")).Types], Complete: true);

        var verdicts = await RunAsync("a.sln", "b.slnx");

        Assert.Equal(
            [("Shared", "0000001"), ("OnlyA", "0000002"), ("OnlyB", "0000003")],
            verdicts.Select(verdict => (verdict.Problem.Symbol.Value, verdict.Problem.Id.Value)));
    }

    [Fact]
    public async Task AProblemStaysRedWithoutAResolution()
    {
        provider.Codebases["a.sln"] = FakeFacts.WithIds(FakeFacts.With(("First", 11, "first")), "First", "0000001");

        var verdict = Assert.Single(await RunAsync("a.sln"));

        Assert.Equal(FindingState.Red, verdict.State);
        Assert.Null(verdict.Problem.Resolution);
    }

    [Fact]
    public async Task ARenameCarriesTheIdOverThroughTheCommentWhenTheOldSymbolIsGone()
    {
        provider.Codebases["a.sln"] = FakeFacts.With(("Old", 11, "body"));
        await RunAsync("a.sln");
        provider.Codebases["a.sln"] = FakeFacts.WithIds(FakeFacts.With(("New", 11, "body")), "New", "0000001");

        var verdict = Assert.Single(await RunAsync("a.sln"));

        Assert.Equal("0000001", verdict.Problem.Id.Value);
        Assert.Equal("New", verdict.Problem.Symbol.Value);
        Assert.Equal(CodeFingerprint.Of(new("New"), "body"), verdict.Problem.Fingerprint);
        var file = ReasonsFile.Load(repo.Root);
        Assert.Equal(1, file.Granted);
        Assert.Equal(["New"], file.Problems.Select(problem => problem.Symbol.Value));
    }

    [Fact]
    public async Task AnIdCopiedOntoOtherCodeWhileItsSymbolExistsGetsANewId()
    {
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 11, "first"));
        await RunAsync("a.sln");
        provider.Codebases["a.sln"] = FakeFacts.WithIds(FakeFacts.With(("First", 11, "first"), ("Copy", 11, "copy")), "Copy", "0000001");

        var verdicts = await RunAsync("a.sln");

        Assert.Equal(
            [("First", "0000001"), ("Copy", "0000002")],
            verdicts.Select(verdict => (verdict.Problem.Symbol.Value, verdict.Problem.Id.Value)));
    }

    [Fact]
    public async Task AProblemIsGreenOnlyWithAResolutionForItsCodeAndItsIdAtTheCode()
    {
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 11, "first"));
        await RunAsync("a.sln");
        var problem = ReasonsFile.Load(repo.Root).Problems.Single();
        var resolution = new Resolution("a flat dispatch", problem.Fingerprint, problem.Value);
        ReasonsFile.Load(repo.Root).With(problem with { Resolution = resolution }).Save(repo.Root);

        var withoutComment = Assert.Single(await RunAsync("a.sln"));
        provider.Codebases["a.sln"] = FakeFacts.WithIds(FakeFacts.With(("First", 11, "first")), "First", "0000001");
        var withComment = Assert.Single(await RunAsync("a.sln"));
        provider.Codebases["a.sln"] = FakeFacts.WithIds(FakeFacts.With(("First", 11, "first changed")), "First", "0000001");
        var afterChange = Assert.Single(await RunAsync("a.sln"));

        Assert.Equal(FindingState.Red, withoutComment.State);
        Assert.Equal(FindingState.Green, withComment.State);
        Assert.Equal(FindingState.Red, afterChange.State);
        Assert.Equal("0000001", afterChange.Problem.Id.Value);
        Assert.Equal(resolution, afterChange.Problem.Resolution);
    }

    [Fact]
    public async Task ARenameOpensTheProblemAgainThoughItsCodeDoesNotChange()
    {
        provider.Codebases["a.sln"] = FakeFacts.WithIds(FakeFacts.With(("Old", 11, "body")), "Old", "0000001");
        await RunAsync("a.sln");
        var problem = ReasonsFile.Load(repo.Root).Problems.Single();
        ReasonsFile.Load(repo.Root).With(problem with { Resolution = new Resolution("a flat dispatch", problem.Fingerprint, problem.Value) }).Save(repo.Root);
        var silent = Assert.Single(await RunAsync("a.sln"));
        provider.Codebases["a.sln"] = FakeFacts.WithIds(FakeFacts.With(("New", 11, "body")), "New", "0000001");

        var renamed = Assert.Single(await RunAsync("a.sln"));

        Assert.Equal(FindingState.Green, silent.State);
        Assert.Equal(("0000001", FindingState.Red), (renamed.Problem.Id.Value, renamed.State));
    }

    [Fact]
    public async Task AClassProblemIsGreenWithItsIdAnywhereInTheClassAndRedAgainOnAnyOtherChange()
    {
        const string Split = "class C\n{\n    int a, b;\n    void X() { a++; }\n    void Y() { b++; }\n}";
        provider.Codebases["a.sln"] = await CSharpSnippet.FactsAsync(Split);
        var red = Assert.Single(await RunAsync("a.sln"));
        var resolution = new Resolution("two jobs by design", red.Problem.Fingerprint, red.Problem.Value);
        ReasonsFile.Load(repo.Root).With(red.Problem with { Resolution = resolution }).Save(repo.Root);

        provider.Codebases["a.sln"] = await CSharpSnippet.FactsAsync(Split.Replace("int a, b;", "int a, b;\n    // navarone: 0000001", StringComparison.Ordinal));
        var commented = Assert.Single(await RunAsync("a.sln"));
        provider.Codebases["a.sln"] = await CSharpSnippet.FactsAsync("// navarone: 0000001\n" + Split.Replace("b++", "b += 2", StringComparison.Ordinal));
        var changed = Assert.Single(await RunAsync("a.sln"));

        Assert.Equal("lcom4", red.Problem.Check);
        Assert.Equal(FindingState.Red, red.State);
        Assert.Equal(FindingState.Green, commented.State);
        Assert.Equal(FindingState.Red, changed.State);
        Assert.Equal("0000001", changed.Problem.Id.Value);
    }

    [Fact]
    public async Task AFlatDispatchIsAFalsePositiveGreenWithoutAReason()
    {
        var cases = string.Concat(Enumerable.Range(1, 11).Select(number => $"case {number}: return {number * 2};\n"));
        provider.Codebases["a.sln"] = await CSharpSnippet.FactsAsync($"class C {{ int Kind(int k) {{ switch (k) {{ {cases} default: return 0; }} }} }}");

        var verdict = Assert.Single(await RunAsync("a.sln"), verdict => verdict.Problem.Check == "cyclomatic");

        Assert.Equal(FindingState.Green, verdict.State);
        Assert.Null(verdict.Problem.Resolution);
    }

    [Fact]
    public async Task ControlCouplingOnAMappingIsAFalsePositiveGreenWithoutAReasonAndOnADispatchThatActsItIsRed()
    {
        provider.Codebases["a.sln"] = await CSharpSnippet.FactsAsync("""
            enum K { A, B }
            class C
            {
                static string Name(K k) => k switch { K.A => "a", K.B => "b", _ => "other" };
                static void Run(K k) { switch (k) { case K.A: Log("a"); break; default: Log("b"); break; } }
                static void Log(string text) { }
            }
            """);

        var verdicts = (await RunAsync("a.sln")).Where(verdict => verdict.Problem.Check == "control-coupling").ToList();

        var mapping = Assert.Single(verdicts, verdict => verdict.Problem.Symbol.Value.Contains("Name", StringComparison.Ordinal));
        var acting = Assert.Single(verdicts, verdict => verdict.Problem.Symbol.Value.Contains("Run", StringComparison.Ordinal));
        Assert.Equal(FindingState.Green, mapping.State);
        Assert.Equal(FindingState.Red, acting.State);
    }

    [Fact]
    public async Task AReasonForASplitThatNamesEveryHelperSilencesIt()
    {
        await File.WriteAllTextAsync(Path.Combine(repo.Root, NavaroneConfig.FileName), """{ "limits": { "cyclomatic": 3, "split-complexity": 5 } }""", TestContext.Current.CancellationToken);
        provider.Codebases["a.sln"] = await CSharpSnippet.FactsAsync(Split);

        var red = Assert.Single(await RunAsync("a.sln"), SplitComplexity);
        var rejection = await Analysis.ResolveAsync(Setup(), ["a.sln"], new ProblemId("0000001"), "Run, Left and Right each handle one stage of the request pipeline", TestContext.Current.CancellationToken);

        Assert.Equal(FindingState.Red, red.State);
        Assert.Null(rejection);
        Assert.Equal(FindingState.Green, Assert.Single(await RunAsync("a.sln"), SplitComplexity).State);
    }

    [Fact]
    public async Task AReasonForASplitThatLeavesOutAHelperIsRejected()
    {
        await File.WriteAllTextAsync(Path.Combine(repo.Root, NavaroneConfig.FileName), """{ "limits": { "cyclomatic": 3, "split-complexity": 5 } }""", TestContext.Current.CancellationToken);
        provider.Codebases["a.sln"] = await CSharpSnippet.FactsAsync(Split);

        var rejection = await Analysis.ResolveAsync(Setup(), ["a.sln"], new ProblemId("0000001"), "Run and Left each handle one stage of the request pipeline", TestContext.Current.CancellationToken);

        Assert.Equal("the reason does not answer for the helpers Right; a reason for a split names every helper that adds a decision", rejection);
        Assert.Equal(FindingState.Red, Assert.Single(await RunAsync("a.sln"), SplitComplexity).State);
    }

    [Fact]
    public async Task ADeterministicRuleRulesOnEveryRunSoItReachesUnchangedCode()
    {
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 11, "first"));
        var red = Assert.Single(await RunAsync("a.sln"));
        var facts = provider.Codebases["a.sln"];
        provider.Codebases["a.sln"] = new([.. facts.Types.Select(type => type with { Methods = [.. type.Methods.Select(method => method with { Dispatch = Dispatch.Flat })] })], Complete: true);

        var flat = Assert.Single(await RunAsync("a.sln"));

        Assert.Equal(FindingState.Red, red.State);
        Assert.Equal(red.Problem.Fingerprint, flat.Problem.Fingerprint);
        Assert.Equal(FindingState.Green, flat.State);
    }

    [Fact]
    public async Task RefusesAReasonsFileThatIsNotValidJson()
    {
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 11, "first"));
        await File.WriteAllTextAsync(Path.Combine(repo.Root, ReasonsFile.FileName), "{ not json", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => RunAsync("a.sln"));
    }

    // Run, at cyclomatic 2, splits into two helpers of cyclomatic 3: inlined it is 6.
    private const string Split = """
        class C
        {
            // navarone: 0000001
            void Run(int a) { if (a > 0) { } Left(a); Right(a); }
            void Left(int a) { if (a > 1) { } if (a > 2) { } }
            void Right(int a) { if (a > 3) { } if (a > 4) { } }
        }
        """;

    private static bool SplitComplexity(Verdict verdict) => verdict.Problem.Check == "split-complexity";

    private EngineSetup Setup() => events.Isolate(Wiring.For(repo.Root, events.Handler)) with { Provider = provider };

    private async Task<IReadOnlyList<Verdict>> RunAsync(params string[] paths)
    {
        var run = await Analysis.RunAsync(Setup(), paths, Coverage.Full, TestContext.Current.CancellationToken);
        return run.Verdicts;
    }

}
