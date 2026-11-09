using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Navarone.Tests.Cli;

/// <summary>
/// Where the built navarone writes its event log, run as a child process with an environment of its own: XDG_DATA_HOME
/// is process-wide, so no test in this process may set it.
/// </summary>
public sealed class DataHomeTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("navarone-datahome-").FullName;

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public async Task TheEventLogLandsUnderNavaroneInTheDataHomeThoughTheDataHomeDoesNotExistYet()
    {
        var dataHome = Path.Combine(root, "not-yet", "share");
        var work = Directory.CreateDirectory(Path.Combine(root, "work")).FullName;

        await RunNavaroneAsync(work, dataHome, "init");

        var logs = Directory.GetFiles(Path.Combine(dataHome, "navarone", "events"), "*.jsonl");
        Assert.Single(logs);
    }

    // Runs the navarone.dll next to this test's assembly with the given arguments, in an environment of only temporary directories.
    private async Task RunNavaroneAsync(string workingDirectory, string dataHome, params string[] args)
    {
        // The runtime directory is <dotnet root>/shared/Microsoft.NETCore.App/<version>/.
        var runtime = RuntimeEnvironment.GetRuntimeDirectory().TrimEnd(Path.DirectorySeparatorChar);
        var dotnet = Path.Combine(Directory.GetParent(runtime)!.Parent!.Parent!.FullName, "dotnet");
        var start = new ProcessStartInfo(dotnet) { WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "navarone.dll"));
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment.Clear();
        start.Environment["HOME"] = Path.Combine(root, "home");
        start.Environment["PATH"] = "/usr/bin:/bin";
        start.Environment["XDG_DATA_HOME"] = dataHome;
        start.Environment["BRAIN_HOME"] = Path.Combine(root, "brain");
        start.Environment["CLAUDE_CONFIG_DIR"] = Path.Combine(root, "claude");
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var errors = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        var printed = await output + await errors;
        Assert.True(process.ExitCode == 0, printed);
    }
}
