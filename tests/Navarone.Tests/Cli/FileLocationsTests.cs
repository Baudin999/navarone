using Navarone.Cli;
using Navarone.Engine.Configuration;
using Navarone.Languages.CSharp;
using Navarone.Tests.Support;

namespace Navarone.Tests.Cli;

public sealed class FileLocationsTests : IDisposable
{
    private readonly TempRepo repo = new();

    public void Dispose() => repo.Dispose();

    [Fact]
    public void FindsNoCodebaseInARepoWithoutASolution()
    {
        File.WriteAllText(Path.Combine(repo.Root, "README.md"), "");

        Assert.Empty(FileLocations.Codebases(repo.Root, new CSharpProvider(Ignores.Nothing(repo.Root))));
    }

    [Fact]
    public void FindsSolutionsInSubfoldersButNotInBinObjOrHiddenDirectories()
    {
        foreach (var path in new[] { "src/A.slnx", "B.sln", "deep/er/C.sln", "bin/X.sln", "src/obj/Y.sln", ".git/Z.sln", ".hidden/W.sln" })
        {
            var full = Path.Combine(repo.Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "");
        }

        Assert.Equal(
            [Path.Combine(repo.Root, "B.sln"), Path.Combine(repo.Root, "deep/er/C.sln"), Path.Combine(repo.Root, "src/A.slnx")],
            FileLocations.Codebases(repo.Root, new CSharpProvider(Ignores.Nothing(repo.Root))));
    }

    [Fact]
    public void SkipsSymbolicLinksSoALoopOrAForeignTreeIsNotWalked()
    {
        Directory.CreateDirectory(Path.Combine(repo.Root, "src"));
        File.WriteAllText(Path.Combine(repo.Root, "src", "A.sln"), "");
        Directory.CreateSymbolicLink(Path.Combine(repo.Root, "src", "loop"), repo.Root);

        Assert.Equal([Path.Combine(repo.Root, "src", "A.sln")], FileLocations.Codebases(repo.Root, new CSharpProvider(Ignores.Nothing(repo.Root))));
    }
}
