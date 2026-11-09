using Navarone.Cli;
using Navarone.Engine;
using Navarone.Engine.Configuration;
using Navarone.Engine.Events;
using Navarone.Engine.Reasons;
using Navarone.Tests.Support;

namespace Navarone.Tests.Cli;

/// <summary>
/// `navarone` shows the signals in the code the change since the default branch touches, and `navarone --full-coverage`
/// every signal. Each test runs in a real git repo whose C.cs holds the code below, committed on master.
/// </summary>
public sealed class TouchedCodeTests : IDisposable
{
    // Run and Walk each have a cyclomatic and a control-coupling signal under a cyclomatic limit of 3, C has an lcom4
    // signal, as Run reads only a and Walk only b, and D.Jump has a cyclomatic and a control-coupling signal.
    private const string Source = """
        class C
        {
            int a;
            int b;

            void Run(int x)
            {
                if (x > a) { }
                if (x > 1) { }
                if (x > 2) { }
            }

            void Walk(int x)
            {
                if (x > b) { }
                if (x > 3) { }
                if (x > 4) { }
            }
        }

        class D
        {
            void Jump(int x)
            {
                if (x > 5) { }
                if (x > 6) { }
                if (x > 7) { }
            }
        }
        """;

    private readonly TempRepo repo = new();
    private readonly TempEvents events = new();
    private readonly FakeProvider provider = new();
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public void Dispose()
    {
        output.Dispose();
        error.Dispose();
        repo.Dispose();
        events.Dispose();
    }

    [Fact]
    public async Task ACleanTreeOnTheDefaultBranchShowsNothingAndExitsZeroYetRecordsEveryProblem()
    {
        await CommitOnMasterAsync(Source);
        await WorkingTreeAsync(("C.cs", Source));

        Assert.Equal(ExitCodes.AllGreen, await RunAsync());

        Assert.Empty(output.ToString());
        Assert.Empty(error.ToString());
        Assert.Equal(7, ReasonsFile.Load(repo.Root).Problems.Count);
    }

    [Fact]
    public async Task AMethodTheBranchChangesShowsItsSignalsAndItsClassesAndNoOther()
    {
        await CommitOnMasterAsync(Source);
        repo.Git("checkout", "-q", "-b", "work");
        var changed = Source.Replace("x > 1", "x > 9", StringComparison.Ordinal);
        await CommitAsync(changed);
        await WorkingTreeAsync(("C.cs", changed));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync());

        Assert.Equal(["cyclomatic C.Run", "lcom4 C", "control-coupling C.Run"], Shown());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task FullCoverageShowsEverySignalOnTheSameBranch()
    {
        await CommitOnMasterAsync(Source);
        repo.Git("checkout", "-q", "-b", "work");
        var changed = Source.Replace("x > 1", "x > 9", StringComparison.Ordinal);
        await CommitAsync(changed);
        await WorkingTreeAsync(("C.cs", changed));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync("--full-coverage"));

        Assert.Equal(7, Shown().Count);
    }

    [Fact]
    public async Task ALineRemovedFromAMethodInTheWorkingTreeShowsItsSignals()
    {
        await CommitOnMasterAsync(Source);
        var changed = Source.Replace("        if (x > 4) { }\n", "", StringComparison.Ordinal);
        await WorkingTreeAsync(("C.cs", changed));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync());

        Assert.Equal(["lcom4 C", "control-coupling C.Walk"], Shown());
    }

    [Fact]
    public async Task ALineOfAClassOutsideItsMethodsShowsOnlyTheClassSignals()
    {
        await CommitOnMasterAsync(Source);
        var changed = Source.Replace("int b;", "int b = 0;", StringComparison.Ordinal);
        await WorkingTreeAsync(("C.cs", changed));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync());

        Assert.Equal(["lcom4 C"], Shown());
    }

    [Fact]
    public async Task AnUntrackedFileShowsAllItsSignals()
    {
        const string Added = """
            class E
            {
                void Hop(int x)
                {
                    if (x > 0) { }
                    if (x > 1) { }
                    if (x > 2) { }
                }
            }
            """;
        await CommitOnMasterAsync(Source);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "E.cs"), Added, TestContext.Current.CancellationToken);
        await WorkingTreeAsync(("C.cs", Source), ("E.cs", Added));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync());

        Assert.Equal(["cyclomatic E.Hop", "control-coupling E.Hop"], Shown());
    }

    [Fact]
    public async Task AChangedHelperShowsTheSplitComplexitySignalOfTheMethodItHelps()
    {
        const string Split = """
            class S
            {
                void Run(int a)
                {
                    if (a > 0) { }
                    Left(a);
                    Right(a);
                }

                void Left(int a)
                {
                    if (a > 1) { }
                    if (a > 2) { }
                }

                void Right(int a)
                {
                    if (a > 3) { }
                    if (a > 4) { }
                }
            }
            """;
        await CommitOnMasterAsync(Split, """{ "limits": { "cyclomatic": 3, "split-complexity": 5 } }""");
        var changed = Split.Replace("a > 2", "a > 9", StringComparison.Ordinal);
        await WorkingTreeAsync(("C.cs", changed));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync());

        Assert.Equal(["control-coupling S.Left", "split-complexity S.Run"], Shown().Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AFieldOfASecondPartialDeclarationShowsTheClassSignals()
    {
        const string Part = "partial class C\n{\n    int c;\n}\n";
        var partial = Source.Replace("class C", "partial class C", StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "P.cs"), Part, TestContext.Current.CancellationToken);
        await CommitOnMasterAsync(partial);
        var changed = Part.Replace("int c;", "int c = 1;", StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "P.cs"), changed, TestContext.Current.CancellationToken);
        await WorkingTreeAsync(("C.cs", partial), ("P.cs", changed));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync());

        Assert.Equal(["lcom4 C"], Shown());
    }

    [Fact]
    public async Task TwoCodebasesThatNameTheirChangedFilesByOnePathShowTheSignalsOfBoth()
    {
        const string Shape = """
            class K
            {
                void First(int x)
                {
                    if (x > 0) { }
                    if (x > 1) { }
                    if (x > 2) { }
                }

                void Second(int x)
                {
                    if (x > 3) { }
                    if (x > 4) { }
                    if (x > 5) { }
                }
            }
            """;
        repo.Git("init", "-q", "-b", "master");
        await File.WriteAllTextAsync(Path.Combine(repo.Root, NavaroneConfig.FileName), """{ "limits": { "cyclomatic": 3 } }""", TestContext.Current.CancellationToken);
        foreach (var (directory, type) in new[] { ("a", "A"), ("b", "B") })
        {
            Directory.CreateDirectory(Path.Combine(repo.Root, directory, "src"));
            await File.WriteAllTextAsync(Path.Combine(repo.Root, directory, $"{directory}.sln"), "", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(repo.Root, directory, "src", "X.cs"), Shape.Replace("class K", $"class {type}", StringComparison.Ordinal), TestContext.Current.CancellationToken);
        }

        repo.Git("add", ".");
        repo.Commit();
        var inA = Shape.Replace("class K", "class A", StringComparison.Ordinal).Replace("x > 1", "x > 9", StringComparison.Ordinal);
        var inB = Shape.Replace("class K", "class B", StringComparison.Ordinal).Replace("x > 4", "x > 9", StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "a", "src", "X.cs"), inA, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "b", "src", "X.cs"), inB, TestContext.Current.CancellationToken);
        provider.Codebases[Path.Combine(repo.Root, "a", "a.sln")] = await CSharpSnippet.FactsAsync(("src/X.cs", inA));
        provider.Codebases[Path.Combine(repo.Root, "b", "b.sln")] = await CSharpSnippet.FactsAsync(("src/X.cs", inB));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync());

        Assert.Contains("cyclomatic A.First", Shown());
        Assert.Contains("cyclomatic B.Second", Shown());
    }

    [Fact]
    public async Task ARepoWithoutTheDefaultBranchIsAUsageError()
    {
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "C.cs"), Source, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "a.sln"), "", TestContext.Current.CancellationToken);
        repo.Git("init", "-q", "-b", "trunk");
        repo.Git("add", ".");
        repo.Commit();
        await WorkingTreeAsync(("C.cs", Source));

        Assert.Equal(ExitCodes.UsageError, await RunAsync());

        Assert.Equal(
            "navarone: no change to show: HEAD shares no commit with the default branch, the one origin/HEAD names, master or main; navarone --full-coverage shows every signal",
            Assert.Single(error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)));
        Assert.Empty(output.ToString());
    }

    [Fact]
    public async Task AMasterThatMovedOnSinceTheBranchLeftItDoesNotChangeWhatTheBranchChanged()
    {
        await CommitOnMasterAsync(Source);
        repo.Git("checkout", "-q", "-b", "work");
        var changed = Source.Replace("x > 1", "x > 9", StringComparison.Ordinal);
        await CommitAsync(changed);
        repo.Git("checkout", "-q", "master");
        await CommitAsync(Source.Replace("x > 3", "x > 8", StringComparison.Ordinal));
        repo.Git("checkout", "-q", "work");
        await WorkingTreeAsync(("C.cs", changed));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync());

        Assert.Equal(["cyclomatic C.Run", "lcom4 C", "control-coupling C.Run"], Shown());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task AMainBranchIsTheDefaultBranchWhenThereIsNoMaster()
    {
        await CommitOnAsync("main", Source);
        repo.Git("checkout", "-q", "-b", "work");
        var changed = Source.Replace("x > 1", "x > 9", StringComparison.Ordinal);
        await CommitAsync(changed);
        await WorkingTreeAsync(("C.cs", changed));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync());

        Assert.Equal(["cyclomatic C.Run", "lcom4 C", "control-coupling C.Run"], Shown());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task TheBranchOriginHeadNamesIsTheDefaultBranchWhateverItIsCalled()
    {
        await CommitOnAsync("trunk", Source);
        repo.Git("update-ref", "refs/remotes/origin/trunk", "HEAD");
        repo.Git("symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/trunk");
        repo.Git("checkout", "-q", "-b", "work");
        var changed = Source.Replace("x > 1", "x > 9", StringComparison.Ordinal);
        await CommitAsync(changed);
        await WorkingTreeAsync(("C.cs", changed));

        Assert.Equal(ExitCodes.AnyRed, await RunAsync());

        Assert.Equal(["cyclomatic C.Run", "lcom4 C", "control-coupling C.Run"], Shown());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task ADirectoryOutsideEveryGitRepoIsAUsageErrorThatSaysThereIsNoChangeToShow()
    {
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "a.sln"), "", TestContext.Current.CancellationToken);
        await WorkingTreeAsync(("C.cs", Source));

        Assert.Equal(ExitCodes.UsageError, await RunAsync());

        Assert.Equal(
            "navarone: no change to show: HEAD shares no commit with the default branch, the one origin/HEAD names, master or main; navarone --full-coverage shows every signal",
            Assert.Single(error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)));
        Assert.Empty(output.ToString());
    }

    [Fact]
    public async Task AGitDiffThatFailsIsAnErrorThatCarriesGitsStderrNotAnEmptyChange()
    {
        await CommitOnMasterAsync(Source);
        repo.Git("checkout", "-q", "-b", "work");
        var changed = Source.Replace("x > 1", "x > 9", StringComparison.Ordinal);
        await WorkingTreeAsync(("C.cs", changed));

        // merge-base reads no index, so the branch point is found; git diff reads it and fails.
        await File.WriteAllTextAsync(Path.Combine(repo.Root, ".git", "index"), "corrupt", TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.UsageError, await RunAsync());

        var line = Assert.Single(error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.StartsWith("navarone: git diff failed with exit 128: ", line, StringComparison.Ordinal);
        Assert.Contains("index file", line, StringComparison.Ordinal);
        Assert.Empty(output.ToString());
    }

    // A repo on master whose one commit holds the given code as C.cs, an empty a.sln and a navarone.json with the
    // given limits, a cyclomatic limit of 3 unless given.
    private Task CommitOnMasterAsync(string code, string config = """{ "limits": { "cyclomatic": 3 } }""") => CommitOnAsync("master", code, config);

    // The same repo on the given branch.
    private async Task CommitOnAsync(string branch, string code, string config = """{ "limits": { "cyclomatic": 3 } }""")
    {
        repo.Git("init", "-q", "-b", branch);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, NavaroneConfig.FileName), config, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "a.sln"), "", TestContext.Current.CancellationToken);
        await CommitAsync(code);
    }

    // Commits the given code as C.cs, with everything else in the working tree.
    private async Task CommitAsync(string code)
    {
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "C.cs"), code, TestContext.Current.CancellationToken);
        repo.Git("add", ".");
        repo.Commit();
    }

    // Writes C.cs as the given files hold it and has a.sln load as their facts, which name each file by its name. A
    // test writes any other file itself.
    private async Task WorkingTreeAsync(params (string Name, string Code)[] files)
    {
        var code = files.Single(file => file.Name == "C.cs").Code;
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "C.cs"), code, TestContext.Current.CancellationToken);
        provider.Codebases[Path.Combine(repo.Root, "a.sln")] = await CSharpSnippet.FactsAsync(files);
    }

    // The check and symbol of each line printed, the symbol without its assembly and parameters.
    private List<string> Shown() =>
    [
        .. output.ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' '))
            .Select(words => $"{words[1]} {words[2].Split("::")[^1].Split('(')[0]}"),
    ];

    private EngineSetup Wire(string repoRoot, NavaroneEventHandler log) => events.Isolate(Wiring.For(repoRoot, log)) with { Provider = provider };

    private Task<int> RunAsync(params string[] args) => Commands.RunAsync(args, repo.Root, events.Handler, Wire, output, error);
}
