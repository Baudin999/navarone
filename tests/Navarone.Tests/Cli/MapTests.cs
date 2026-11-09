using System.Text.Json.Nodes;
using Navarone.Cli;
using Navarone.Engine;
using Navarone.Engine.Events;
using Navarone.Tests.Support;

namespace Navarone.Tests.Cli;

/// <summary>`navarone map --json`: the methods a lens can name and the red problems, printed without writing anything.</summary>
public sealed class MapTests : IDisposable
{
    private const string Shop = """
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

            public int Price(string text)
            {
                return text.Length;
            }

            private int Base(int a) => a is { } value ? value + tax : 0;

            public int Tax { get; set; }
        }
        """;

    private readonly TempRepo repo = new();
    private readonly TempEvents events = new();
    private readonly FakeProvider provider = new();
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public MapTests() => Directory.CreateDirectory(Path.Combine(repo.Root, ".git"));

    public void Dispose()
    {
        output.Dispose();
        error.Dispose();
        repo.Dispose();
        events.Dispose();
    }

    [Fact]
    public async Task TheMapListsEachMethodWithItsLensNameLinesAndTypeAndLeavesOutAccessors()
    {
        await ShopAsync();

        Assert.Equal(ExitCodes.Printed, await RunAsync("map", "--json"));

        var map = JsonNode.Parse(output.ToString())!;
        var file = Assert.Single(map["files"]!.AsArray())!;
        Assert.Equal("Shop.cs", (string?)file["path"]);
        var methods = file["methods"]!.AsArray().Select(method => ((string?)method!["name"], (int)method["firstLine"]!, (int)method["lastLine"]!, (string?)method["type"]));
        Assert.Equal(
            [("Shop.Price:1", 5, 16, "Shop"), ("Shop.Price:2", 18, 21, "Shop"), ("Shop.Base", 23, 23, "Shop")],
            methods);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task TheMapListsTheRedProblemsWithTheMethodTheySitInAndGrantsNoId()
    {
        await ShopAsync();

        await RunAsync("map", "--json");

        var map = JsonNode.Parse(output.ToString())!;
        var problems = map["problems"]!.AsArray();
        Assert.All(problems, problem => Assert.Null(problem!["id"]));
        Assert.Equal(
            [("Shop.cs", 5, 16, "Shop.Price:1", "cyclomatic", "red"), ("Shop.cs", 5, 16, "Shop.Price:1", "control-coupling", "red")],
            problems.Select(problem => ((string?)problem!["path"], (int)problem["firstLine"]!, (int)problem["lastLine"]!, (string?)problem["method"], (string?)problem["check"], (string?)problem["state"])));
        Assert.False(File.Exists(Path.Combine(repo.Root, "reasons.json")));
        Assert.Empty(events.Handler.Read());
        Assert.Equal(repo.Git("rev-parse", "HEAD").Trim(), (string?)map["commit"]);
    }

    [Fact]
    public async Task TheMapNamesTheIdTheCheckGrantedAndTheFilesTheWorkingTreeChanges()
    {
        await ShopAsync();
        await RunAsync("--full-coverage");
        output.GetStringBuilder().Clear();
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "Other.txt"), "x", TestContext.Current.CancellationToken);

        await RunAsync("map", "--json");

        var map = JsonNode.Parse(output.ToString())!;
        Assert.Equal(["0000001", "0000002"], map["problems"]!.AsArray().Select(problem => (string?)problem!["id"]));
        Assert.Equal(repo.Git("rev-parse", "HEAD").Trim(), (string?)map["commit"]);
        Assert.Equal(["Other.txt", "reasons.json"], map["changed"]!.AsArray().Select(path => (string?)path).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task TheMapOutsideAGitRepositoryHasNoCommitAndNoChangedFiles()
    {
        Directory.Delete(Path.Combine(repo.Root, ".git"));
        var solution = Path.Combine(repo.Root, "a.sln");
        await File.WriteAllTextAsync(solution, "", TestContext.Current.CancellationToken);
        provider.Codebases[solution] = FakeFacts.With(("First", 1, "first"));

        Assert.Equal(ExitCodes.Printed, await RunAsync("map", "--json"));

        var map = JsonNode.Parse(output.ToString())!;
        Assert.Null(map["commit"]);
        Assert.Empty(map["changed"]!.AsArray());
        Assert.Equal("T.First", (string?)map["files"]![0]!["methods"]![0]!["name"]);
    }

    private async Task ShopAsync()
    {
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
