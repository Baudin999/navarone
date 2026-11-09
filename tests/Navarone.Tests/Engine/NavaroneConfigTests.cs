using Navarone.Engine.Configuration;
using Navarone.Tests.Support;

namespace Navarone.Tests.Engine;

public sealed class NavaroneConfigTests : IDisposable
{
    private readonly TempRepo repo = new();

    public void Dispose() => repo.Dispose();

    [Fact]
    public void HoldsTheDefaultsWithoutAFile() =>
        Assert.Equal(10, NavaroneConfig.Load(repo.Root).Limits.For("cyclomatic"));

    [Fact]
    public void HoldsTheDefaultForACheckTheFileDoesNotName()
    {
        Write("""{ "limits": { "cognitive": 12 } }""");

        Assert.Equal(10, NavaroneConfig.Load(repo.Root).Limits.For("cyclomatic"));
        Assert.Equal(12, NavaroneConfig.Load(repo.Root).Limits.For("cognitive"));
    }

    [Fact]
    public void TakesTheLimitTheFileSets()
    {
        Write("""{ "limits": { "cyclomatic": 7 } }""");

        Assert.Equal(7, NavaroneConfig.Load(repo.Root).Limits.For("cyclomatic"));
    }

    [Fact]
    public void TakesALimitAtItsDefault()
    {
        Write("""{ "limits": { "cognitive": 15 } }""");

        Assert.Equal(15, NavaroneConfig.Load(repo.Root).Limits.For("cognitive"));
    }

    [Theory]
    [InlineData("cyclomatic", 11)]
    [InlineData("cognitive", 1000)]
    [InlineData("lcom4", 2)]
    [InlineData("control-coupling", 1)]
    [InlineData("split-complexity", 11)]
    public void RefusesALimitAboveItsDefault(string checkId, int limit)
    {
        Write($$"""{ "limits": { "{{checkId}}": {{limit}} } }""");

        var refusal = Assert.Throws<InvalidDataException>(() => NavaroneConfig.Load(repo.Root));
        Assert.Contains($"raises the limit of '{checkId}' to {limit}", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesALimitForACheckThatDoesNotExist()
    {
        Write("""{ "limits": { "cyclomatc": 12 } }""");

        Assert.Throws<InvalidDataException>(() => NavaroneConfig.Load(repo.Root));
    }

    [Fact]
    public void RefusesAFileThatIsNotValidJson()
    {
        Write("{ \"limits\": ");

        Assert.Throws<InvalidDataException>(() => NavaroneConfig.Load(repo.Root));
    }

    [Fact]
    public void IgnoresNothingWithoutAFile()
    {
        var ignores = NavaroneConfig.Load(repo.Root).Ignores;

        Assert.False(ignores.IgnoresProject("Navarone.Tests"));
        Assert.False(ignores.IgnoresFile(Path.Combine(repo.Root, "A.cs")));
    }

    [Fact]
    public void TakesTheIgnoresTheFileSets()
    {
        Write("""{ "ignore": { "files": ["src/Generated/**"], "projects": ["*.Tests"] } }""");

        var ignores = NavaroneConfig.Load(repo.Root).Ignores;

        Assert.True(ignores.IgnoresProject("Navarone.Tests"));
        Assert.False(ignores.IgnoresProject("Navarone.Engine"));
        Assert.True(ignores.IgnoresFile(Path.Combine(repo.Root, "src", "Generated", "A.cs")));
        Assert.False(ignores.IgnoresFile(Path.Combine(repo.Root, "src", "A.cs")));
    }

    [Fact]
    public void TakesTheLimitsBesideTheIgnores()
    {
        Write("""{ "limits": { "cyclomatic": 7 }, "ignore": { "projects": ["*.Tests"] } }""");

        Assert.Equal(7, NavaroneConfig.Load(repo.Root).Limits.For("cyclomatic"));
    }

    [Theory]
    [InlineData("""{ "ignores": { "projects": ["*.Tests"] } }""")]
    [InlineData("""{ "ignore": { "project": ["*.Tests"] } }""")]
    [InlineData("""{ "ignore": { "projects": [null] } }""")]
    [InlineData("""{ "ignore": { "files": [""] } }""")]
    [InlineData("""{ "ignore": ["*.Tests"] }""")]
    public void RefusesAnIgnoreItCannotRead(string json)
    {
        Write(json);

        Assert.Throws<InvalidDataException>(() => NavaroneConfig.Load(repo.Root));
    }

    [Fact]
    public void InitWritesTheTestProjectsIgnored()
    {
        Assert.True(NavaroneConfig.Init(repo.Root));

        var ignores = NavaroneConfig.Load(repo.Root).Ignores;
        Assert.True(ignores.IgnoresProject("Navarone.Tests"));
        Assert.True(ignores.IgnoresProject("Shop.Test"));
        Assert.False(ignores.IgnoresProject("Navarone.Engine"));
        Assert.Equal(NavaroneConfig.Initial, File.ReadAllText(Path.Combine(repo.Root, NavaroneConfig.FileName)));
    }

    [Fact]
    public void InitLeavesANavaroneJsonThatIsThere()
    {
        const string Mine = """{ "limits": { "cyclomatic": 7 } }""";
        Write(Mine);

        Assert.False(NavaroneConfig.Init(repo.Root));

        Assert.Equal(Mine, File.ReadAllText(Path.Combine(repo.Root, NavaroneConfig.FileName)));
    }

    [Fact]
    public void InitLeavesNoTemporaryFileBehind()
    {
        NavaroneConfig.Init(repo.Root);

        Assert.Equal([NavaroneConfig.FileName], Directory.GetFileSystemEntries(repo.Root).Select(Path.GetFileName));
    }

    private void Write(string json) => File.WriteAllText(Path.Combine(repo.Root, NavaroneConfig.FileName), json);
}
