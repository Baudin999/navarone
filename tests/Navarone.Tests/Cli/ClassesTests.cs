using System.Text.Json.Nodes;
using Navarone.Cli;
using Navarone.Engine;
using Navarone.Engine.Events;
using Navarone.Tests.Support;

namespace Navarone.Tests.Cli;

/// <summary>`navarone classes &lt;path&gt; --json`: every type in a source file with its members and their pieces, printed without writing anything.</summary>
public sealed class ClassesTests : IDisposable
{
    private const string Till = """
        namespace Shop.Orders;

        public interface IPriced
        {
            int Price(int a);
        }

        public class Till
        {
            private int tax;
            private string name = "";

            public int Count { get; set; }

            public Till(int tax)
            {
                this.tax = tax;
            }

            public int Total(int a) => a + tax;

            public string Label() => Name();

            private string Name() => name;

            public int Rate
            {
                get { return tax; }
            }

            public class Drawer
            {
            }
        }
        """;

    private const string Split = """
        public class Gauge
        {
            private int read;
            private int written;

            public int Level
            {
                get { return read; }
                private set { written = value; }
            }
        }

        public record Point(int Y, int X);
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
    public async Task EachTypeInTheFileHasItsAssemblyNamespaceLinesAndTheTypeItIsNestedIn()
    {
        await TillAsync();

        Assert.Equal(ExitCodes.Printed, await RunAsync("classes", "Till.cs", "--json"));

        var view = JsonNode.Parse(output.ToString())!;
        Assert.Equal("Till.cs", (string?)view["path"]);
        Assert.Equal(repo.Git("rev-parse", "HEAD").Trim(), (string?)view["commit"]);
        var types = view["types"]!.AsArray().Select(type => ((string?)type!["assembly"], (string?)type["namespace"], (string?)type["name"], (string?)type["containingType"], (int)type["firstLine"]!, (int)type["lastLine"]!));
        Assert.Equal(
            [("Snippet", "Shop.Orders", "IPriced", null, 3, 6), ("Snippet", "Shop.Orders", "Till", null, 8, 34), ("Snippet", "Shop.Orders", "Drawer", "Till", 31, 33)],
            types);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task EachMemberHasItsKindWhetherItIsPublicItsLinesItsLensNameAndItsPiece()
    {
        await TillAsync();

        await RunAsync("classes", "Till.cs", "--json");

        var till = TypeNamed("Till");
        var members = till["members"]!.AsArray().Select(member => ((string?)member!["name"], (string?)member["kind"], (bool)member["public"]!, (int)member["firstLine"]!, (int)member["lastLine"]!, (string?)member["lens"], (int?)member["piece"]));
        Assert.Equal(
            [
                ("tax", "field", false, 10, 10, null, 1),
                ("name", "field", false, 11, 11, null, 2),
                ("Count", "property", true, 13, 13, null, null),
                (".ctor(int)", "constructor", true, 15, 18, null, null),
                ("Total(int)", "method", true, 20, 20, "Till.Total", 1),
                ("Label()", "method", true, 22, 22, "Till.Label", 2),
                ("Name()", "method", false, 24, 24, "Till.Name", 2),
                ("Rate", "property", true, 26, 29, null, 1),
            ],
            members);
        Assert.All(till["members"]!.AsArray().SkipLast(1), member => Assert.Empty(member!["accessors"]!.AsArray()));
        var rate = Assert.Single(till["members"]!.AsArray()[^1]!["accessors"]!.AsArray())!;
        Assert.Equal(("get", true, 28, 28, 1), ((string?)rate["name"], (bool)rate["public"]!, (int)rate["firstLine"]!, (int)rate["lastLine"]!, (int?)rate["piece"]));
    }

    [Fact]
    public async Task APropertyWhoseAccessorsFallInTwoPiecesIsInNoneAndEachAccessorNamesItsOwn()
    {
        await CommitAsync(Split);

        await RunAsync("classes", "Till.cs", "--json");

        var level = TypeNamed("Gauge")["members"]!.AsArray().Single(member => (string?)member!["name"] == "Level")!;
        Assert.Equal(("property", true, 6, 10, null), ((string?)level["kind"], (bool)level["public"]!, (int)level["firstLine"]!, (int)level["lastLine"]!, (int?)level["piece"]));
        var accessors = level["accessors"]!.AsArray().Select(accessor => ((string?)accessor!["name"], (bool)accessor["public"]!, (int)accessor["firstLine"]!, (int)accessor["lastLine"]!, (int?)accessor["piece"]));
        Assert.Equal([("get", true, 8, 8, 1), ("set", false, 9, 9, 2)], accessors);
    }

    [Fact]
    public async Task MembersOnOneLineComeInTheOrderWritten()
    {
        await CommitAsync(Split);

        await RunAsync("classes", "Till.cs", "--json");

        var members = TypeNamed("Point")["members"]!.AsArray().Select(member => ((string?)member!["name"], (string?)member["kind"]));
        Assert.Equal([(".ctor(int, int)", "constructor"), ("Y", "property"), ("X", "property")], members);
    }

    [Fact]
    public async Task AClassOfTwoPiecesIsAboveTheLimitAndAnInterfaceIsNotMeasured()
    {
        await TillAsync();

        await RunAsync("classes", "Till.cs", "--json");

        var till = TypeNamed("Till")["pieces"]!;
        Assert.Equal(("lcom4", 2, 1, true), ((string?)till["check"], (int)till["value"]!, (int)till["limit"]!, (bool)till["above"]!));
        var drawer = TypeNamed("Drawer")["pieces"]!;
        Assert.Equal(("lcom4", 0, 1, false), ((string?)drawer["check"], (int)drawer["value"]!, (int)drawer["limit"]!, (bool)drawer["above"]!));
        var priced = TypeNamed("IPriced");
        Assert.Null(priced["pieces"]);
        var price = Assert.Single(priced["members"]!.AsArray())!;
        Assert.Equal(("Price(int)", "IPriced.Price", null), ((string?)price["name"], (string?)price["lens"], (int?)price["piece"]));
    }

    [Fact]
    public async Task TheViewGrantsNoIdAndLogsNothing()
    {
        await TillAsync();

        await RunAsync("classes", "Till.cs", "--json");

        Assert.False(File.Exists(Path.Combine(repo.Root, "reasons.json")));
        Assert.Empty(events.Handler.Read());
    }

    [Fact]
    public async Task ASolutionInASubdirectoryTakesThePathRelativeToTheRepo()
    {
        await TillAsync("sub");

        await RunAsync("classes", "sub/Till.cs", "--json");

        var view = JsonNode.Parse(output.ToString())!;
        Assert.Equal("sub/Till.cs", (string?)view["path"]);
        Assert.Equal(3, view["types"]!.AsArray().Count);
    }

    [Fact]
    public async Task AFileWithNoTypeHasNone()
    {
        await TillAsync();
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "Notes.txt"), "x", TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Printed, await RunAsync("classes", "Notes.txt", "--json"));

        Assert.Empty(JsonNode.Parse(output.ToString())!["types"]!.AsArray());
    }

    [Fact]
    public async Task APathToNoFileIsAUsageError()
    {
        await TillAsync();

        await UsageErrorAsync("Gone.cs");
    }

    [Fact]
    public async Task AFileOutsideTheRepoIsAUsageError()
    {
        await TillAsync();

        await UsageErrorAsync(typeof(ClassesTests).Assembly.Location);
    }

    private async Task UsageErrorAsync(string path)
    {
        Assert.Equal(ExitCodes.UsageError, await RunAsync("classes", path, "--json"));

        Assert.Equal($"navarone: no file {path} in the repo", error.ToString().Trim());
        Assert.Empty(output.ToString());
    }

    private JsonNode TypeNamed(string name) =>
        JsonNode.Parse(output.ToString())!["types"]!.AsArray().Single(type => (string?)type!["name"] == name)!;

    private Task TillAsync(string directory = "") => CommitAsync(Till, directory);

    private async Task CommitAsync(string code, string directory = "")
    {
        repo.Git("init", "-q", "-b", "master");
        Directory.CreateDirectory(Path.Combine(repo.Root, directory));
        var solution = Path.Combine(repo.Root, directory, "a.sln");
        await File.WriteAllTextAsync(solution, "", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, directory, "Till.cs"), code, TestContext.Current.CancellationToken);
        repo.Git("add", ".");
        repo.Commit();
        provider.Codebases[solution] = await CSharpSnippet.FactsAsync(("Till.cs", code));
    }

    private EngineSetup Wire(string repoRoot, NavaroneEventHandler log) => events.Isolate(Wiring.For(repoRoot, log)) with { Provider = provider };

    private Task<int> RunAsync(params string[] args) => Commands.RunAsync(args, repo.Root, events.Handler, Wire, output, error);
}
