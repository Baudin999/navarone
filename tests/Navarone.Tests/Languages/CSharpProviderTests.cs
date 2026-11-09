using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Languages.CSharp;
using Navarone.Tests.Support;

namespace Navarone.Tests.Languages;

/// <summary>The provider as the CLI wires it: it reads navarone.json from disk and loads a real solution through MSBuild.</summary>
public sealed class CSharpProviderTests : IDisposable
{
    private const string Project = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private readonly TempRepo repo = new();

    public void Dispose() => repo.Dispose();

    [Fact]
    public async Task TheProviderReadsNoFactFromAnIgnoredProjectOrAnIgnoredFileOfARealSolution()
    {
        await WriteSolutionAsync();
        await WriteAsync(NavaroneConfig.FileName, """{ "ignore": { "files": ["App/Generated/*.cs"], "projects": ["*.Tests"] } }""");

        var facts = await LoadAsync();

        Assert.Equal(["Shop"], facts.Types.Select(type => type.Name));
    }

    [Fact]
    public async Task WithoutAnyIgnoreTheProviderReadsEveryProjectAndFile()
    {
        await WriteSolutionAsync();

        var facts = await LoadAsync();

        Assert.Equal(["Order", "Shop", "ShopTests"], facts.Types.Select(type => type.Name).Order(StringComparer.Ordinal));
    }

    // A solution of App, with Shop and a Generated/Order file, and App.Tests, with ShopTests.
    private async Task WriteSolutionAsync()
    {
        await WriteAsync("a.slnx", """
            <Solution>
              <Project Path="App/App.csproj" />
              <Project Path="App.Tests/App.Tests.csproj" />
            </Solution>
            """);
        await WriteAsync("App/App.csproj", Project);
        await WriteAsync("App/Shop.cs", "class Shop { public int Price() => 1; }");
        await WriteAsync("App/Generated/Order.cs", "class Order { public void Pay() { } }");
        await WriteAsync("App.Tests/App.Tests.csproj", Project);
        await WriteAsync("App.Tests/ShopTests.cs", "class ShopTests { public void PricesOne() { } }");
    }

    // The solution as the CLI wires the provider: with the ignores navarone.json sets.
    private async Task<CodebaseFacts> LoadAsync()
    {
        var provider = new CSharpProvider(NavaroneConfig.Load(repo.Root).Ignores);
        return await provider.LoadAsync(Path.Combine(repo.Root, "a.slnx"), TestContext.Current.CancellationToken);
    }

    private async Task WriteAsync(string relative, string text)
    {
        var path = Path.Combine(repo.Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text, TestContext.Current.CancellationToken);
    }
}
