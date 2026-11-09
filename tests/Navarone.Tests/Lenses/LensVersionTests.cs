using Navarone.Engine.Lenses;
using Navarone.Tests.Support;

namespace Navarone.Tests.Lenses;

public sealed class LensVersionTests : IDisposable
{
    private readonly TempRepo repo = new();

    public void Dispose() => repo.Dispose();

    [Fact]
    public void OutsideAGitRepositoryTheLineSaysSo()
    {
        Assert.Equal("The code is not in a git repository, so it has no version.\n\n", LensVersion.Of(repo.Root));
    }

    [Fact]
    public void ARepositoryWithoutACommitSaysSo()
    {
        repo.Git("init", "-q", "-b", "master");

        Assert.Equal("The code is in a git repository with no commit yet, so it has no version.\n\n", LensVersion.Of(repo.Root));
    }

    [Fact]
    public async Task ACleanTreeNamesTheFullHashOfHead()
    {
        await CommitAsync("T.cs");

        Assert.Equal($"At commit {repo.Git("rev-parse", "HEAD").Trim()}.\n\n", LensVersion.Of(repo.Root));
    }

    [Fact]
    public async Task ATreeWithUncommittedChangesListsTheTrackedAndUntrackedPaths()
    {
        await CommitAsync("T.cs");
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "T.cs"), "changed", TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(repo.Root, "sub"));
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "sub", "New.cs"), "new", TestContext.Current.CancellationToken);

        var line = LensVersion.Of(repo.Root);

        Assert.StartsWith($"At commit {repo.Git("rev-parse", "HEAD").Trim()}, with uncommitted changes, so the code shown is the working tree, not the commit.", line, StringComparison.Ordinal);
        Assert.EndsWith("\n\n- `T.cs`\n- `sub/New.cs`\n\n", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadingTheVersionWritesNothingNotEvenTheIndex()
    {
        await CommitAsync("T.cs");
        var index = Path.Combine(repo.Root, repo.Git("rev-parse", "--git-path", "index").Trim());
        File.SetLastWriteTimeUtc(Path.Combine(repo.Root, "T.cs"), DateTime.UtcNow.AddMinutes(1));
        var time = File.GetLastWriteTimeUtc(index);
        var bytes = File.ReadAllBytes(index);

        LensVersion.Of(repo.Root);

        Assert.Equal(time, File.GetLastWriteTimeUtc(index));
        Assert.Equal(bytes, File.ReadAllBytes(index));
    }

    [Fact]
    public async Task AGitStatusThatFailsThrowsWithGitsStderrInsteadOfReadingACleanTree()
    {
        await CommitAsync("T.cs");
        await File.WriteAllTextAsync(Path.Combine(repo.Root, ".git", "index"), "corrupt", TestContext.Current.CancellationToken);

        var thrown = Assert.Throws<InvalidDataException>(() => LensVersion.Of(repo.Root));

        Assert.StartsWith("git status failed with exit 128: ", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("index file", thrown.Message, StringComparison.Ordinal);
    }

    private async Task CommitAsync(string file)
    {
        repo.Git("init", "-q", "-b", "master");
        await File.WriteAllTextAsync(Path.Combine(repo.Root, file), "original", TestContext.Current.CancellationToken);
        repo.Git("add", file);
        repo.Commit();
    }
}
