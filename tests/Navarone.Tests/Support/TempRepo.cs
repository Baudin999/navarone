using System.Diagnostics;

namespace Navarone.Tests.Support;

/// <summary>
/// A directory that stands for the root of an analysed repo, deleted after the test. The git it runs reads neither the user's global config nor the system's.
/// </summary>
internal sealed class TempRepo : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("navarone-tests-").FullName;

    /// <summary>Runs git in the repo and returns its stdout. A git that exits with anything but 0 fails the test.</summary>
    public string Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = Root, RedirectStandardOutput = true };

        // The user's global and system git config must not reach a test repo.
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        return output;
    }

    /// <summary>Commits what is staged, as a test user.</summary>
    public void Commit() => Git("-c", "user.name=test", "-c", "user.email=test@example.org", "commit", "-qm", "step");

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
