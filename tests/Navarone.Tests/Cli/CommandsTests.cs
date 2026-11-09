using System.Text.Json.Nodes;
using Navarone.Cli;
using Navarone.Engine;
using Navarone.Engine.Configuration;
using Navarone.Engine.Events;
using Navarone.Engine.Reasons;
using Navarone.Tests.Support;

namespace Navarone.Tests.Cli;

public sealed class CommandsTests : IDisposable
{
    private const string Sound = "a flat dispatch over the eleven token kinds the lexer emits";

    private readonly TempRepo repo = new();
    private readonly TempEvents events = new();
    private readonly FakeProvider provider = new();
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public CommandsTests() => Directory.CreateDirectory(Path.Combine(repo.Root, ".git"));

    public void Dispose()
    {
        output.Dispose();
        error.Dispose();
        repo.Dispose();
        events.Dispose();
    }

    [Theory]
    [InlineData("events", "foo")]
    [InlineData("lnes", "x")]
    [InlineData("foo", "keep")]
    [InlineData("12345", "a reason")]
    [InlineData("00000012", "a reason")]
    [InlineData("000000a", "a reason")]
    [InlineData("foo", "large", "text")]
    [InlineData("foo", "settled", "keep")]
    [InlineData("foo", "replace", "3", "finding")]
    [InlineData("evidence", "foo", "--json")]
    public async Task AWordThatIsNotAnIdsShapeIsAUsageErrorAndLogsNoReason(params string[] args)
    {
        await CodebaseAsync("0000001");
        await RunAsync("--full-coverage");
        var logged = events.Handler.Read().Count;
        error.GetStringBuilder().Clear();

        Assert.Equal(ExitCodes.UsageError, await RunAsync(args));

        Assert.StartsWith("usage: navarone", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(logged, events.Handler.Read().Count);
        Assert.Null(ReasonsFile.Load(repo.Root).For(new ProblemId("0000001"))!.Resolution);
    }

    [Fact]
    public async Task ARepoWithoutASolutionIsAUsageError()
    {
        Assert.Equal(ExitCodes.UsageError, await RunAsync());
        Assert.StartsWith("navarone: no .sln or .slnx under the repo root", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "limits": { "cyclomatc": 12 } }""")]
    [InlineData("""{ "limits": """)]
    [InlineData("""{ "limits": { "cognitive": 1000 } }""")]
    [InlineData("""{ "ignores": { "projects": ["*.Tests"] } }""")]
    public async Task ANavaroneJsonNavaroneCannotReadIsAUsageErrorOnOneLine(string json)
    {
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "A.sln"), "", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, NavaroneConfig.FileName), json, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.UsageError, await RunAsync());
        var line = Assert.Single(Lines(error));
        Assert.StartsWith("navarone: navarone.json", line, StringComparison.Ordinal);
        Assert.Empty(output.ToString());
    }

    [Fact]
    public async Task InitWritesNavaroneJsonWithTheTestProjectsIgnoredAndAGitignoreThatIgnoresTheCacheAndNoOtherFile()
    {
        var before = Directory.GetFileSystemEntries(repo.Root);

        Assert.Equal(ExitCodes.Initialized, await RunAsync("init"));

        var path = Path.Combine(repo.Root, NavaroneConfig.FileName);
        var ignore = Path.Combine(repo.Root, ".gitignore");
        Assert.Equal(NavaroneConfig.Initial, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(".navarone/\n", await File.ReadAllTextAsync(ignore, TestContext.Current.CancellationToken));
        Assert.Equal(before.Append(path).Append(ignore).Order(StringComparer.Ordinal), Directory.GetFileSystemEntries(repo.Root).Order(StringComparer.Ordinal));
        Assert.Equal([$"{path} written, with the test projects ignored", $"{ignore} ignores .navarone/, the facts cache"], Lines(output));
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task InitLeavesANavaroneJsonThatIsThereAndSaysSo()
    {
        const string Mine = """{ "limits": { "cyclomatc": 12 } }""";
        var path = Path.Combine(repo.Root, NavaroneConfig.FileName);
        await File.WriteAllTextAsync(path, Mine, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Initialized, await RunAsync("init"));

        Assert.Equal(Mine, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal($"{path} exists already; init leaves it as it is", Lines(output)[0]);
        Assert.Empty(error.ToString());
    }

    [Theory]
    [InlineData("bin/\nobj/", "bin/\nobj/\n.navarone/\n")]
    [InlineData("bin/\r\nobj/", "bin/\r\nobj/\r\n.navarone/\r\n")]
    [InlineData("", ".navarone/\n")]
    public async Task InitAddsTheCacheToAGitignoreThatDoesNotIgnoreIt(string mine, string added)
    {
        var ignore = Path.Combine(repo.Root, ".gitignore");
        await File.WriteAllTextAsync(ignore, mine, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Initialized, await RunAsync("init"));

        Assert.Equal(added, await File.ReadAllTextAsync(ignore, TestContext.Current.CancellationToken));
        Assert.Equal($"{ignore} ignores .navarone/, the facts cache", Lines(output)[1]);
    }

    [Theory]
    [InlineData("bin/\n.navarone/\n")]
    [InlineData("  .navarone/  ")]
    [InlineData("/.navarone/\r\nobj/")]
    [InlineData(".navarone")]
    public async Task InitLeavesAGitignoreThatIgnoresTheCacheAsItIsAndSaysSo(string mine)
    {
        var ignore = Path.Combine(repo.Root, ".gitignore");
        await File.WriteAllTextAsync(ignore, mine, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Initialized, await RunAsync("init"));
        Assert.Equal(ExitCodes.Initialized, await RunAsync("init"));

        Assert.Equal(mine, await File.ReadAllTextAsync(ignore, TestContext.Current.CancellationToken));
        Assert.Equal($"{ignore} ignores .navarone/ already", Lines(output)[1]);
        Assert.Equal($"{ignore} ignores .navarone/ already", Lines(output)[3]);
    }

    [Fact]
    public async Task TheCheckAndTheLensPrintTheSameWithTheCacheAsWithout()
    {
        await CachedCodebaseAsync();
        var real = provider.Codebases[Path.Combine(repo.Root, "a.sln")];
        string[][] commands = [["--full-coverage"], ["lens", "method", "Shop.Price", "callers", "expand:2", "class:short"], ["lens", "--json", "method", "Shop.Base", "callers"]];
        foreach (var command in commands)
        {
            // The first run loads the facts and writes reasons.json, which changes the version of the code; the second
            // loads them again for that version and writes them to the cache.
            await RunAsync(command);
            output.GetStringBuilder().Clear();
            await RunAsync(command);
            var fresh = output.ToString();
            Assert.NotEmpty(fresh);
            output.GetStringBuilder().Clear();
            provider.Codebases[Path.Combine(repo.Root, "a.sln")] = new([], Complete: true);

            await RunAsync(command);

            Assert.Equal(fresh, output.ToString());
            output.GetStringBuilder().Clear();
            provider.Codebases[Path.Combine(repo.Root, "a.sln")] = real;
        }

        Assert.NotEmpty(Directory.GetFiles(Path.Combine(repo.Root, ".navarone"), "*.json"));
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task ARedProblemIsPrintedWithItsIdCheckSymbolPlaceAndQuestion()
    {
        await CodebaseAsync();

        Assert.Equal(ExitCodes.AnyRed, await RunAsync("--full-coverage"));

        var line = Assert.Single(Lines(output));
        Assert.StartsWith("0000001 cyclomatic First at T.cs:1: ", line, StringComparison.Ordinal);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task AResolvedReasonExitsZeroAndTheProblemIsNoLongerPrinted()
    {
        await CodebaseAsync("0000001");

        Assert.Equal(ExitCodes.Resolved, await RunAsync("0000001", Sound));
        Assert.Equal("0000001 resolved: the problem stays silent while its code is unchanged", Assert.Single(Lines(output)));
        Assert.Equal(ExitCodes.AllGreen, await RunAsync("--full-coverage"));
        Assert.Single(Lines(output));
    }

    [Fact]
    public async Task ARejectedReasonExitsOneAndSaysWhy()
    {
        await CodebaseAsync();

        Assert.Equal(ExitCodes.Rejected, await RunAsync("0000001", Sound));
        Assert.StartsWith("0000001 rejected: the code of First carries no", Assert.Single(Lines(output)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownIdIsAUsageError()
    {
        await CodebaseAsync("0000001");

        Assert.Equal(ExitCodes.UsageError, await RunAsync("0000009", Sound));
        Assert.Equal("navarone: no problem with id 0000009 in the code as it is now, and no refused fix of it on this worktree", Assert.Single(Lines(error)));
    }

    [Fact]
    public async Task EvidencePrintsTheProblemsEvidenceDocumentAndWritesNothing()
    {
        await CodebaseAsync("0000001");
        await RunAsync("--full-coverage");
        var reasons = await File.ReadAllTextAsync(Path.Combine(repo.Root, "reasons.json"), TestContext.Current.CancellationToken);
        output.GetStringBuilder().Clear();

        Assert.Equal(ExitCodes.Printed, await RunAsync("evidence", "0000001", "--json"));

        var document = JsonNode.Parse(output.ToString())!;
        Assert.Equal("""{"path":"T.cs","startLine":1,"endLine":1,"code":"first"}""", document["code"]!.ToJsonString());
        Assert.Equal("""{"check":"cyclomatic","symbol":"First","value":12,"limit":10,"base":1}""", document["finding"]!.ToJsonString());
        var proof = document["proof"]!.AsArray();
        Assert.Equal(11, proof.Count);
        Assert.Equal("""{"span":{"startLine":1,"startCol":1,"endLine":1,"endCol":2,"text":"if"},"kind":"if","adds":1,"why":"a branch adds one path","parts":[]}""", proof[0]!.ToJsonString());
        Assert.Equal(reasons, await File.ReadAllTextAsync(Path.Combine(repo.Root, "reasons.json"), TestContext.Current.CancellationToken));
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task EvidenceForAnUnknownIdOrACheckWithoutEvidenceIsAUsageError()
    {
        var solution = Path.Combine(repo.Root, "a.sln");
        await File.WriteAllTextAsync(solution, "", TestContext.Current.CancellationToken);
        provider.Codebases[solution] = FakeFacts.With(("First", 16, "first"));
        await RunAsync("--full-coverage");

        Assert.Equal(ExitCodes.UsageError, await RunAsync("evidence", "0000009", "--json"));
        Assert.Equal(ExitCodes.UsageError, await RunAsync("evidence", "0000002", "--json"));
        Assert.Equal(["navarone: no problem with id 0000009 in the code as it is now", "navarone: the cognitive check builds no evidence yet"], Lines(error));
    }

    [Fact]
    public async Task LensPrintsWhatTheLensShowsAndWritesNothing()
    {
        await CodebaseAsync();

        Assert.Equal(ExitCodes.Printed, await RunAsync("lens", "method", "T.First", "callers:2", "[expand:1]", "class:short"));

        Assert.StartsWith("The code is not in a git repository, so it has no version.\n\n# method T.First, overload 1 of 1\n\n`T.cs:1`\n\n```fake\nfirst\n```\n\n## callers:2\n\n", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("## class:short\n\n### T\n\n", output.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(repo.Root, "reasons.json")));
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task LensJsonPrintsTheCommitAndTheSectionsAndWritesNothing()
    {
        await CodebaseAsync();
        repo.Git("init", "-q", "-b", "master");
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "T.cs"), "first", TestContext.Current.CancellationToken);
        repo.Git("add", "T.cs");
        repo.Commit();
        var before = repo.Git("status", "--porcelain");

        Assert.Equal(ExitCodes.Printed, await RunAsync("lens", "--json", "method", "T.First", "class:short"));

        var page = JsonNode.Parse(Assert.Single(Lines(output)))!;
        var subject = Assert.Single(page["sections"]!.AsArray())!;
        var view = Assert.Single(subject["sections"]!.AsArray())!;
        var type = Assert.Single(view["sections"]!.AsArray())!;
        var used = Assert.Single(type["sections"]!.AsArray())!;
        JsonNode[] sections = [subject, view, type, used];
        Assert.Equal(repo.Git("rev-parse", "HEAD").Trim(), (string?)page["commit"]);
        Assert.Equal(["method T.First, overload 1 of 1", "class:short", "T", "Fields and properties it uses"], sections.Select(section => (string?)section["title"]));
        Assert.Equal([true, false, false, false], sections.Select(section => (bool)section["open"]!));
        var code = Assert.Single(subject["blocks"]!.AsArray())!;
        Assert.Equal(("code", "T.cs", 1, "first"), ((string?)code["type"], (string?)code["path"], (int)code["startLine"]!, (string?)code["code"]));
        Assert.Equal(before, repo.Git("status", "--porcelain"));
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task LensJsonOutsideAGitRepositoryHasNoCommitAndALensItCannotShowIsAUsageError()
    {
        await CodebaseAsync();

        Assert.Equal(ExitCodes.Printed, await RunAsync("lens", "--json", "method", "T.First"));
        Assert.Null(JsonNode.Parse(Assert.Single(Lines(output)))!["commit"]);
        output.GetStringBuilder().Clear();

        Assert.Equal(ExitCodes.UsageError, await RunAsync("lens", "--json", "method", "T.Second"));
        Assert.Equal("navarone: no method T.Second in the codebase, at column 8 of the lens", error.ToString().Split('\n')[0]);
        Assert.Empty(output.ToString());
    }

    [Theory]
    [InlineData(new[] { "lens", "method", "T.Second" }, "navarone: no method T.Second in the codebase, at column 8 of the lens")]
    [InlineData(new[] { "lens", "class", "T" }, "navarone: the kind class is not built yet; only method is, at column 1 of the lens")]
    [InlineData(new[] { "lens", "method", "T.First", "callers:2", "[expand:1" }, "navarone: expected a view, or ] to close the [ at column 26, not the end of the lens, at column 35 of the lens")]
    [InlineData(new[] { "lens" }, "navarone: expected a kind, such as method, not the end of the lens, at column 1 of the lens")]
    public async Task ALensNavaroneCannotShowIsAUsageErrorThatSaysWhereInTheLens(string[] args, string message)
    {
        await CodebaseAsync();

        Assert.Equal(ExitCodes.UsageError, await RunAsync(args));

        Assert.Equal(message, error.ToString().Split('\n')[0]);
        Assert.Empty(output.ToString());
    }

    [Fact]
    public async Task DisputesJsonPrintsAnEmptyListFromAnyDirectory()
    {
        var outside = Directory.CreateTempSubdirectory("navarone-outside-").FullName;
        try
        {
            Assert.Equal(ExitCodes.Listed, await Commands.RunAsync(["disputes", "--json"], outside, events.Handler, Wire, output, error));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }

        Assert.Equal("""{"source":"navarone","questions":[]}""", Assert.Single(Lines(output)));
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task DuplicatesPrintsTheFloorAndThePairsAndWritesNothing()
    {
        await CodebaseAsync();

        Assert.Equal(ExitCodes.Printed, await RunAsync("duplicates", "--similarity", "0.9", "--tokens", "40"));

        Assert.StartsWith("0 clone pairs at similarity 0.90 or more and 40 tokens or more.", output.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(repo.Root, "reasons.json")));
        Assert.Empty(error.ToString());
    }

    [Theory]
    [InlineData("--similarity 1.5")]
    [InlineData("--tokens 0")]
    [InlineData("--tokens")]
    [InlineData("--tokens 30 --tokens 40")]
    [InlineData("--lines 3")]
    public async Task DuplicatesWithAnOptionItCannotReadIsAUsageError(string options)
    {
        await CodebaseAsync();

        Assert.Equal(ExitCodes.UsageError, await RunAsync(["duplicates", .. options.Split(' ')]));
        Assert.Equal("navarone: duplicates takes --similarity <share from 0 to 1> and --tokens <count of 1 or more>", Assert.Single(Lines(error)));
    }

    private static string[] Lines(StringWriter writer) => writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    // A solution at the repo root whose one method, First, has one cyclomatic problem and the given ids at its code.
    private async Task CodebaseAsync(params string[] idsAtFirst)
    {
        var solution = Path.Combine(repo.Root, "a.sln");
        await File.WriteAllTextAsync(solution, "", TestContext.Current.CancellationToken);
        provider.Codebases[solution] = FakeFacts.WithIds(FakeFacts.With(("First", 11, "first")), "First", idsAtFirst);
    }

    // A solution at the root of a git repo with one commit, whose facts are those of real C# with a cyclomatic problem in
    // Shop.Price, which calls Shop.Base.
    private async Task CachedCodebaseAsync()
    {
        const string Shop = """
            class Shop
            {
                private int tax;

                public int Price(int a)
                {
                    if (a > 1 && a < 100 || a == -1) { tax++; }
                    if (a > 2) { tax++; }
                    if (a > 3) { tax++; }
                    if (a > 4) { tax++; }
                    if (a > 5) { tax++; }
                    if (a > 6) { tax++; }
                    if (a > 7) { tax++; }
                    if (a > 8) { tax++; }
                    return a switch { > 9 and < 20 => Base(a), < 0 => 0, _ => tax };
                }

                private int Base(int a) => a is { } value ? value + tax : 0;
            }
            """;
        repo.Git("init", "-q", "-b", "master");
        var solution = Path.Combine(repo.Root, "a.sln");
        await File.WriteAllTextAsync(solution, "", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "Shop.cs"), Shop, TestContext.Current.CancellationToken);
        repo.Git("add", "a.sln", "Shop.cs");
        repo.Commit();
        provider.Codebases[solution] = await CSharpSnippet.FactsAsync(("Shop.cs", Shop));
    }

    private EngineSetup Wire(string repoRoot, NavaroneEventHandler log) => events.Isolate(Wiring.For(repoRoot, log)) with { Provider = provider };

    private Task<int> RunAsync(params string[] args) => Commands.RunAsync(args, repo.Root, events.Handler, Wire, output, error);
}
