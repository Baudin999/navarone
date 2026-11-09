using Navarone.Classifiers.Deterministic;
using Navarone.Cli;
using Navarone.Engine;
using Navarone.Engine.Facts;
using Navarone.Engine.Reasons;
using Navarone.Tests.Support;

namespace Navarone.Tests.Engine;

public sealed class ResolutionTests : IDisposable
{
    private const string Sound = "a flat dispatch over the eleven token kinds the lexer emits";

    private readonly TempRepo repo = new();
    private readonly TempEvents events = new();
    private readonly FakeProvider provider = new();

    public ResolutionTests() => provider.Codebases["a.sln"] = Commented(("First", 11, "first"), ("Second", 12, "second"));

    public void Dispose()
    {
        repo.Dispose();
        events.Dispose();
    }

    [Fact]
    public async Task AnIdThatMatchesNoProblemIsAnErrorAndStoresNothing()
    {
        var refused = await Assert.ThrowsAsync<InvalidDataException>(() => ResolveAsync("0000009", Sound));

        Assert.Equal("no problem with id 0000009 in the code as it is now, and no refused fix of it on this worktree", refused.Message);
        Assert.All(ReasonsFile.Load(repo.Root).Problems, problem => Assert.Null(problem.Resolution));
    }

    [Fact]
    public async Task AReasonNoRuleRejectsIsStoredWithTheCodeItAnsweredAndSilencesTheProblem()
    {
        Assert.Null(await ResolveAsync("0000001", Sound));

        var stored = ReasonsFile.Load(repo.Root).For(new ProblemId("0000001"))!.Resolution;
        Assert.Equal(new Resolution(Sound, CodeFingerprint.Of(new("First"), "first"), 12), stored);
        Assert.Equal(FindingState.Green, (await StateAsync("First")).State);
        Assert.Equal(FindingState.Red, (await StateAsync("Second")).State);
    }

    [Fact]
    public async Task TouchingTheCodeOpensTheProblemAgainAndANewReasonTakesThePlaceOfTheOld()
    {
        await ResolveAsync("0000001", Sound);
        provider.Codebases["a.sln"] = Commented(("First", 11, "first changed"), ("Second", 12, "second"));

        var opened = await StateAsync("First");
        var rejection = await ResolveAsync("0000001", "the method still maps each of the eleven token kinds to one handler");
        var silenced = await StateAsync("First");

        Assert.Equal(FindingState.Red, opened.State);
        Assert.Null(rejection);
        Assert.Equal(FindingState.Green, silenced.State);
        Assert.Equal(CodeFingerprint.Of(new("First"), "first changed"), silenced.Problem.Resolution!.Fingerprint);
    }

    [Fact]
    public async Task AResolutionNoLongerCountsOnceTheFindingsValueChangesThoughItsCodeDoesNot()
    {
        await ResolveAsync("0000001", Sound);
        provider.Codebases["a.sln"] = Commented(("First", 12, "first"), ("Second", 12, "second"));

        var recounted = await StateAsync("First");

        Assert.Equal((FindingState.Red, 13), (recounted.State, recounted.Problem.Value));
        Assert.Equal(12, recounted.Problem.Resolution!.Value);
    }

    [Fact]
    public async Task AReasonWithoutTheIdAtItsCodeIsRejectedAndNothingIsStored()
    {
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 11, "first"));

        await AssertRejectedAsync("0000001", Sound, "carries no `// navarone: 0000001` comment");
    }

    [Fact]
    public async Task AnIdAtOtherCodeDoesNotCountAsAtTheCode()
    {
        provider.Codebases["a.sln"] = FakeFacts.WithIds(FakeFacts.With(("First", 11, "first"), ("Second", 12, "second")), "Second", "0000001");

        await AssertRejectedAsync("0000001", Sound, "carries no `// navarone: 0000001` comment");
    }

    [Fact]
    public async Task AReasonUnderTheWordFloorIsRejectedAndOneAtItIsStored()
    {
        var shortReason = string.Join(' ', Enumerable.Repeat("word", DeterministicReasonClassifier.WordFloor - 1));
        var atFloor = string.Join(' ', Enumerable.Range(1, DeterministicReasonClassifier.WordFloor).Select(number => $"w{number}"));

        await AssertRejectedAsync("0000001", shortReason, $"has {DeterministicReasonClassifier.WordFloor - 1} words");
        Assert.Null(await ResolveAsync("0000001", atFloor));
    }

    [Fact]
    public async Task AReasonThatRepeatsAResolutionWordForWordIsRejected()
    {
        await ResolveAsync("0000001", Sound);

        await AssertRejectedAsync("0000002", "A flat dispatch, over the eleven token-kinds the LEXER emits!", "repeats a resolution");
        Assert.Equal(FindingState.Red, (await StateAsync("Second")).State);
    }

    [Fact]
    public async Task AReasonRejectedBecauseItsIdWasNotAtTheCodeCanBeGivenAgainOnceItIs()
    {
        provider.Codebases["a.sln"] = FakeFacts.With(("First", 11, "first"));
        await AssertRejectedAsync("0000001", Sound, "carries no `// navarone: 0000001` comment");
        provider.Codebases["a.sln"] = Commented(("First", 11, "first"));

        Assert.Null(await ResolveAsync("0000001", Sound));
        Assert.Equal(FindingState.Green, (await StateAsync("First")).State);
    }

    private static CodebaseFacts Commented(params (string Symbol, int Branches, string Code)[] methods) =>
        FakeFacts.WithIds(FakeFacts.WithIds(FakeFacts.With(methods), "First", "0000001"), "Second", "0000002");

    // The reason is rejected with a why that holds the given text, and the problem keeps the resolution it had.
    private async Task AssertRejectedAsync(string id, string reason, string why)
    {
        var before = ReasonsFile.Load(repo.Root).For(new ProblemId(id))?.Resolution;

        var rejection = await ResolveAsync(id, reason);

        Assert.Contains(why, rejection, StringComparison.Ordinal);
        Assert.Equal(before, ReasonsFile.Load(repo.Root).For(new ProblemId(id))!.Resolution);
    }

    private EngineSetup Setup() => events.Isolate(Wiring.For(repo.Root, events.Handler)) with { Provider = provider };

    private Task<string?> ResolveAsync(string id, string reason) =>
        Analysis.ResolveAsync(Setup(), ["a.sln"], new ProblemId(id), reason, TestContext.Current.CancellationToken);

    private async Task<Verdict> StateAsync(string symbol)
    {
        var run = await Analysis.RunAsync(Setup(), ["a.sln"], Coverage.Full, TestContext.Current.CancellationToken);
        return run.Verdicts.Single(verdict => verdict.Problem.Symbol.Value == symbol && verdict.Problem.Check == "cyclomatic");
    }
}
