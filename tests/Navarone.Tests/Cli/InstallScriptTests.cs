using System.Diagnostics;
using System.Runtime.Versioning;

namespace Navarone.Tests.Cli;

/// <summary>
/// install.sh at the repo root, run by bash against a temporary HOME, LOCAL_BIN, BRAIN_HOME and CLAUDE_CONFIG_DIR,
/// never the real ones.
/// A dotnet stub on a temporary PATH stands in for the publish: it publishes a navarone stub that logs where it ran
/// and with which arguments, so no test builds or analyses anything. install.sh is bash, so not on Windows.
/// </summary>
[UnsupportedOSPlatform("windows")]
public sealed class InstallScriptTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("navarone-install-").FullName;

    public InstallScriptTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Stubs);
        File.WriteAllText(Path.Combine(Stubs, "dotnet"), $$"""
            #!/bin/sh
            while [ $# -gt 0 ]; do [ "$1" = -o ] && out="$2"; shift; done
            mkdir -p "$out"
            printf '#!/bin/sh\necho "$PWD|$*" >>"{{Log}}"\n' >"$out/navarone"
            chmod +x "$out/navarone"

            """);
        File.SetUnixFileMode(Path.Combine(Stubs, "dotnet"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private string Log => Path.Combine(root, "navarone.log");

    private string Home => Path.Combine(root, "home");

    private string Stubs => Path.Combine(root, "stubs");

    private string LocalBin => Path.Combine(root, "local bin");

    private string BrainHome => Path.Combine(root, "brain");

    private string Sources => Path.Combine(BrainHome, "sources.tsv");

    private string ClaudeRules => Path.Combine(root, "claude", "rules");

    private string Lens => $"'{LocalBin}/navarone' lens --json";

    private string Events => $"'{LocalBin}/navarone' events --json";

    private string Code => $"'{LocalBin}/navarone' code";

    private string Classes => $"'{LocalBin}/navarone' classes";

    private string Line => $"navarone\t'{LocalBin}/navarone' disputes --json\t{Lens}\t{Events}\t{Code}\t{Classes}\n";

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public async Task WithBrainInstalledItAddsTheSourcesLineOnceByTheAbsolutePathAndRunsNoNavarone()
    {
        Directory.CreateDirectory(BrainHome);

        await InstallAsync();
        var sources = await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken);
        await InstallAsync();

        Assert.Equal(Line, sources);
        Assert.Equal(sources, await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken));
        Assert.Equal(Path.Combine(Home, ".local", "lib", "navarone", "navarone"), new FileInfo(Path.Combine(LocalBin, "navarone")).LinkTarget);
        Assert.False(File.Exists(Log));
        Assert.False(Directory.Exists(Path.Combine(Home, ".local", "navarone")));
    }

    [Fact]
    public async Task TheCommandInTheSourcesLineRunsTheLinkedNavaroneThroughSh()
    {
        Directory.CreateDirectory(BrainHome);
        await InstallAsync();
        var command = (await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken)).Split('\t')[1].TrimEnd('\n');

        var (code, printed) = await RunAsync("/bin/sh", ["-c", $"set -- {command}; printf '%s|' \"$@\""]);

        Assert.Equal(0, code);
        Assert.Equal($"{LocalBin}/navarone|disputes|--json|", printed);
    }

    [Fact]
    public async Task TheLensCommandInTheSourcesLineTakesTheQueryWordsAsArgumentsUntouched()
    {
        Directory.CreateDirectory(BrainHome);
        await InstallAsync();
        var command = (await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken)).Split('\t')[2].TrimEnd('\n');

        var (code, printed) = await RunAsync("/bin/sh", ["-c", $"set -- {command} \"$@\"; printf '%s|' \"$@\"", "sh", "method", "T.First;rm -rf $HOME `x` '\"", "class:short"]);

        Assert.Equal(0, code);
        Assert.Equal($"{LocalBin}/navarone|lens|--json|method|T.First;rm -rf $HOME `x` '\"|class:short|", printed);
    }

    [Fact]
    public async Task AnExistingNavaroneLineWithAllSixColumnsIsKeptAndSaysSo()
    {
        Directory.CreateDirectory(BrainHome);
        const string own = "navarone\tmy list\tmy lens\tmy events\tmy code\tmy classes\n";
        await File.WriteAllTextAsync(Sources, own, TestContext.Current.CancellationToken);

        var printed = await InstallAsync();

        Assert.Equal(own, await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken));
        Assert.Contains("kept the navarone line", printed);
    }

    [Fact]
    public async Task AnExistingNavaroneLineWithThreeColumnsKeepsThemAndGetsTheEventsCodeAndClassesCommandsOnce()
    {
        Directory.CreateDirectory(BrainHome);
        await File.WriteAllTextAsync(Sources, "navarone\tmy list\tmy lens\n", TestContext.Current.CancellationToken);

        var printed = await InstallAsync();
        var sources = await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken);
        var again = await InstallAsync();

        Assert.Equal($"navarone\tmy list\tmy lens\t{Events}\t{Code}\t{Classes}\n", sources);
        Assert.Contains("completed the navarone line", printed);
        Assert.Equal(sources, await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken));
        Assert.Contains("kept the navarone line", again);
    }

    [Fact]
    public async Task AFullNavaroneLineAsTheLastLineWithoutANewlineIsKeptAsItIs()
    {
        Directory.CreateDirectory(BrainHome);
        const string own = "other\tother list\nnavarone\ta\tb\tc\td\te\tf";
        await File.WriteAllTextAsync(Sources, own, TestContext.Current.CancellationToken);

        var printed = await InstallAsync();

        Assert.Equal(own, await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken));
        Assert.Contains("kept the navarone line", printed);
    }

    [Fact]
    public async Task ABlankColumnAtTheEndOfTheNavaroneLineCountsAsMissing()
    {
        Directory.CreateDirectory(BrainHome);
        await File.WriteAllTextAsync(Sources, "navarone\tmy list\t \n", TestContext.Current.CancellationToken);

        await InstallAsync();

        Assert.Equal($"navarone\tmy list\t{Lens}\t{Events}\t{Code}\t{Classes}\n", await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnExistingNavaroneLineWithFourColumnsGetsOnlyTheCodeAndClassesCommands()
    {
        Directory.CreateDirectory(BrainHome);
        await File.WriteAllTextAsync(Sources, "navarone\tmy list\tmy lens\tmy events\n", TestContext.Current.CancellationToken);

        await InstallAsync();

        Assert.Equal($"navarone\tmy list\tmy lens\tmy events\t{Code}\t{Classes}\n", await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnExistingNavaroneLineWithFiveColumnsGetsOnlyTheClassesCommand()
    {
        Directory.CreateDirectory(BrainHome);
        await File.WriteAllTextAsync(Sources, "navarone\ta\tb\tc\td\n", TestContext.Current.CancellationToken);

        await InstallAsync();

        Assert.Equal($"navarone\ta\tb\tc\td\t{Classes}\n", await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheClassesCommandInTheSourcesLineTakesAPathAndJsonAsArgumentsUntouched()
    {
        Directory.CreateDirectory(BrainHome);
        await InstallAsync();
        var command = (await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken)).Split('\t')[5].TrimEnd('\n');

        var (code, printed) = await RunAsync("/bin/sh", ["-c", $"set -- {command} \"$@\"; printf '%s|' \"$@\"", "sh", "src/My File's.cs", "--json"]);

        Assert.Equal(0, code);
        Assert.Equal($"{LocalBin}/navarone|classes|src/My File's.cs|--json|", printed);
    }

    [Fact]
    public async Task AnExistingNavaroneLineWithoutALensCommandKeepsItsListCommandAndGetsTheLensCommandOnce()
    {
        Directory.CreateDirectory(BrainHome);
        await File.WriteAllTextAsync(Sources, "# my sources\nother\tother list\nnavarone\tmy own navarone --json\n", TestContext.Current.CancellationToken);

        await InstallAsync();
        var sources = await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken);
        await InstallAsync();

        Assert.Equal($"# my sources\nother\tother list\nnavarone\tmy own navarone --json\t{Lens}\t{Events}\t{Code}\t{Classes}\n", sources);
        Assert.Equal(sources, await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ASourcesFileWithoutATrailingNewlineGetsTheLineOnItsOwnLine()
    {
        Directory.CreateDirectory(BrainHome);
        await File.WriteAllTextAsync(Sources, "other\tother list", TestContext.Current.CancellationToken);

        await InstallAsync();

        Assert.Equal($"other\tother list\n{Line}", await File.ReadAllTextAsync(Sources, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WithoutBrainItWritesNoSourcesAndLinksNavarone()
    {
        await InstallAsync();

        Assert.False(Directory.Exists(BrainHome));
        Assert.Equal(Path.Combine(Home, ".local", "lib", "navarone", "navarone"), new FileInfo(Path.Combine(LocalBin, "navarone")).LinkTarget);
        Assert.False(File.Exists(Log));
    }

    [Fact]
    public async Task WithoutBrainItLinksTheRulesToTheCheckoutsNavaroneMd()
    {
        await InstallAsync();
        await InstallAsync();

        Assert.Equal(Path.Combine(Repo, "rules", "NAVARONE.md"), new FileInfo(Path.Combine(ClaudeRules, "NAVARONE.md")).LinkTarget);
    }

    [Fact]
    public async Task ANavaroneMdThatIsNotALinkIsLeftAloneAndTheInstallSaysSo()
    {
        Directory.CreateDirectory(ClaudeRules);
        var own = Path.Combine(ClaudeRules, "NAVARONE.md");
        await File.WriteAllTextAsync(own, "my own rules\n", TestContext.Current.CancellationToken);

        var printed = await InstallAsync();

        Assert.Null(new FileInfo(own).LinkTarget);
        Assert.Equal("my own rules\n", await File.ReadAllTextAsync(own, TestContext.Current.CancellationToken));
        Assert.Contains($"{own} is a file, not a link, so it was left alone", printed);
        Assert.Equal(Path.Combine(Home, ".local", "lib", "navarone", "navarone"), new FileInfo(Path.Combine(LocalBin, "navarone")).LinkTarget);
    }

    [Fact]
    public async Task ATasteMdLinkWhoseTargetIsGoneIsRemovedAndTheInstallSaysSo()
    {
        Directory.CreateDirectory(ClaudeRules);
        var taste = Path.Combine(ClaudeRules, "taste.md");
        File.CreateSymbolicLink(taste, Path.Combine(root, "gone", "taste.md"));

        var printed = await InstallAsync();

        Assert.False(File.Exists(taste));
        Assert.Null(new FileInfo(taste).LinkTarget);
        Assert.Contains($"removed {taste}", printed);
    }

    [Fact]
    public async Task ATasteMdLinkWhoseTargetExistsIsKept()
    {
        Directory.CreateDirectory(ClaudeRules);
        var target = Path.Combine(root, "taste.md");
        await File.WriteAllTextAsync(target, "taste\n", TestContext.Current.CancellationToken);
        var taste = Path.Combine(ClaudeRules, "taste.md");
        File.CreateSymbolicLink(taste, target);

        await InstallAsync();

        Assert.Equal(target, new FileInfo(taste).LinkTarget);
    }

    [Fact]
    public async Task ATasteMdThatIsNotALinkIsKept()
    {
        Directory.CreateDirectory(ClaudeRules);
        var taste = Path.Combine(ClaudeRules, "taste.md");
        await File.WriteAllTextAsync(taste, "my own taste\n", TestContext.Current.CancellationToken);

        await InstallAsync();

        Assert.Equal("my own taste\n", await File.ReadAllTextAsync(taste, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("n\n")]
    [InlineData("no\n")]
    [InlineData("\n")]
    [InlineData("maybe\n")]
    public async Task AnyAnswerButYesChangesNothingAndExitsOne(string answer)
    {
        Directory.CreateDirectory(BrainHome);

        var (code, printed) = await RunAsync("/bin/bash", [Path.Combine(Repo, "install.sh")], answer);

        Assert.Equal(1, code);
        Assert.Contains("Continue? [y/N]", printed);
        Assert.Contains("Nothing was changed.", printed);
        Assert.False(Directory.Exists(Path.Combine(Home, ".local", "lib", "navarone")));
        Assert.False(Directory.Exists(LocalBin));
        Assert.False(Directory.Exists(ClaudeRules));
        Assert.False(File.Exists(Sources));
    }

    [Fact]
    public async Task BeforeItAsksItNamesEveryPathItWillChange()
    {
        Directory.CreateDirectory(BrainHome);

        var (_, printed) = await RunAsync("/bin/bash", [Path.Combine(Repo, "install.sh")]);
        var question = printed.IndexOf("Continue? [y/N]", StringComparison.Ordinal);

        Assert.True(question > 0, printed);
        Assert.InRange(printed.IndexOf(Path.Combine(Home, ".local", "lib", "navarone"), StringComparison.Ordinal), 0, question);
        Assert.InRange(printed.IndexOf(Path.Combine(LocalBin, "navarone"), StringComparison.Ordinal), 0, question);
        Assert.InRange(printed.IndexOf(Path.Combine(ClaudeRules, "NAVARONE.md"), StringComparison.Ordinal), 0, question);
        Assert.InRange(printed.IndexOf("session in every project", StringComparison.Ordinal), 0, question);
        Assert.InRange(printed.IndexOf(Sources, StringComparison.Ordinal), 0, question);
    }

    [Fact]
    public async Task WithoutBrainItDoesNotNameTheSourcesFile()
    {
        var (_, printed) = await RunAsync("/bin/bash", [Path.Combine(Repo, "install.sh")]);

        Assert.DoesNotContain("sources.tsv", printed);
    }

    // The checkout this test was built in, which holds install.sh.
    private static string Repo
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(directory.FullName, "install.sh")))
            {
                directory = directory.Parent!;
            }

            return directory.FullName;
        }
    }

    // Runs install.sh and answers its question with yes.
    private async Task<string> InstallAsync()
    {
        var (code, printed) = await RunAsync("/bin/bash", [Path.Combine(Repo, "install.sh")], "y\n");
        Assert.True(code == 0, printed);
        return printed;
    }

    // Runs the program with an environment of only the temporary directories, the dotnet stub first on PATH, and the
    // input on its stdin, which then closes.
    private async Task<(int Code, string Printed)> RunAsync(string program, string[] args, string input = "")
    {
        var start = new ProcessStartInfo(program) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment.Clear();
        start.Environment["HOME"] = Home;
        start.Environment["PATH"] = $"{Stubs}:/usr/bin:/bin";
        start.Environment["LOCAL_BIN"] = LocalBin;
        start.Environment["BRAIN_HOME"] = BrainHome;
        start.Environment["CLAUDE_CONFIG_DIR"] = Path.Combine(root, "claude");
        start.Environment["XDG_CONFIG_HOME"] = Path.Combine(Home, ".config");
        using var process = Process.Start(start)!;
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var errors = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, await output + await errors);
    }
}
