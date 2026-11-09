using Navarone.Engine.Configuration;

namespace Navarone.Tests.Engine;

public sealed class IgnoresTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "repo");

    [Theory]
    [InlineData("**/*.g.cs", "A.g.cs", true)]
    [InlineData("**/*.g.cs", "src/Deep/A.g.cs", true)]
    [InlineData("src/*.cs", "src/A.cs", true)]
    [InlineData("src/*.cs", "src/Deep/A.cs", false)]
    [InlineData("src/**", "src/Deep/A.cs", true)]
    [InlineData("src/?.cs", "src/A.cs", true)]
    [InlineData("src/?.cs", "src/AB.cs", false)]
    [InlineData("src/A.cs", "src/A.cs", true)]
    [InlineData("src/A.cs", "other/src/A.cs", false)]
    [InlineData("src/A+B.cs", "src/A+B.cs", true)]
    public void AFileGlobMatchesTheWholePathRelativeToTheRoot(string glob, string relative, bool ignored)
    {
        var ignores = new Ignores(Root, [glob], []);

        Assert.Equal(ignored, ignores.IgnoresFile(Path.Combine(Root, relative)));
    }

    [Fact]
    public void AFileOutsideTheRootOrWithoutAPathIsNeverIgnored()
    {
        var ignores = new Ignores(Root, ["**"], []);

        Assert.False(ignores.IgnoresFile(Path.Combine(Path.GetTempPath(), "other", "A.cs")));
        Assert.False(ignores.IgnoresFile(null));
        Assert.False(ignores.IgnoresFile(""));
    }

    [Theory]
    [InlineData("*.Tests", "Navarone.Tests", true)]
    [InlineData("*.Tests", "Navarone.Test", false)]
    [InlineData("*.Test", "Shop.Test", true)]
    [InlineData("*.Tests", "Navarone.Engine", false)]
    [InlineData("Navarone.Tests", "Navarone.Tests", true)]
    public void AProjectGlobMatchesTheWholeName(string glob, string name, bool ignored)
    {
        var ignores = new Ignores(Root, [], [glob]);

        Assert.Equal(ignored, ignores.IgnoresProject(name));
    }

    [Fact]
    public void NothingIgnoresNothing()
    {
        Assert.False(Ignores.Nothing(Root).IgnoresProject("Navarone.Tests"));
        Assert.False(Ignores.Nothing(Root).IgnoresFile(Path.Combine(Root, "A.cs")));
    }
}
