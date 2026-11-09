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
                ("Rate.get", "property", true, 26, 28, null, 1),
            ],
            members);
    }

    [Fact]
    public async Task AClassOfTwoPiecesIsAboveTheLimitAndAnInterfaceIsNotMeasured()
    {
        await TillAsync();

        await RunAsync("classes", "Till.cs", "--json");

        var lcom4 = TypeNamed("Till")["lcom4"]!;
        Assert.Equal((2, 1, true), ((int)lcom4["value"]!, (int)lcom4["limit"]!, (bool)lcom4["above"]!));
        var drawer = TypeNamed("Drawer")["lcom4"]!;
        Assert.Equal((0, 1, false), ((int)drawer["value"]!, (int)drawer["limit"]!, (bool)drawer["above"]!));
        var priced = TypeNamed("IPriced");
        Assert.Null(priced["lcom4"]);
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

    private async Task TillAsync(string directory = "")
    {
        repo.Git("init", "-q", "-b", "master");
        Directory.CreateDirectory(Path.Combine(repo.Root, directory));
        var solution = Path.Combine(repo.Root, directory, "a.sln");
        await File.WriteAllTextAsync(solution, "", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, directory, "Till.cs"), Till, TestContext.Current.CancellationToken);
        repo.Git("add", ".");
        repo.Commit();
        provider.Codebases[solution] = await CSharpSnippet.FactsAsync(("Till.cs", Till));
    }

    private EngineSetup Wire(string repoRoot, NavaroneEventHandler log) => events.Isolate(Wiring.For(repoRoot, log)) with { Provider = provider };

    private Task<int> RunAsync(params string[] args) => Commands.RunAsync(args, repo.Root, events.Handler, Wire, output, error);
}
