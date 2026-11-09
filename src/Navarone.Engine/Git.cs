using System.ComponentModel;
using System.Diagnostics;

namespace Navarone.Engine;

/// <summary>
/// The git checkout of the repo at Root, read only. A question to git, such as which commit HEAD is, gets no answer
/// when git is missing, Root is no git repo or git answers no: null. A read of what changed, by git status, diff or
/// ls-files, never reads a failure as no change: a git that is missing or exits with anything but 0 throws an
/// InvalidDataException with git's stderr. A git command that runs longer than Timeout is killed and throws one too.
/// </summary>
internal sealed record Git(string Root)
{
    // How long one git command may run before it is killed.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The full hash of HEAD, or null when Root is no git repo or the repo has no commit yet.</summary>
    public string? Head() => Ask("rev-parse", "--verify", "--quiet", "HEAD")?.Trim();

    /// <summary>The short name of the branch checked out at Root, or null on a detached HEAD or when Root is no git repo.</summary>
    public string? Branch() => Ask("symbolic-ref", "--quiet", "--short", "HEAD")?.Trim();

    /// <summary>
    /// The main checkout of the repo Root belongs to, which every worktree of the repo shares: the directory that holds
    /// the common .git directory. Null when Root is no git repo. A bare repo has no checkout, so it is the common
    /// directory itself.
    /// </summary>
    public string? MainCheckout()
    {
        if (Ask("rev-parse", "--path-format=absolute", "--git-common-dir")?.Trim() is not { Length: > 0 } common)
        {
            return null;
        }

        var directory = new DirectoryInfo(common);
        if (directory.Name != ".git" || directory.Parent is null)
        {
            return directory.FullName;
        }

        return directory.Parent.FullName;
    }

    /// <summary>Whether Root is inside a git work tree.</summary>
    public bool IsRepo() => Ask("rev-parse", "--is-inside-work-tree") is not null;

    /// <summary>
    /// The paths, relative to the repo, of every tracked or untracked file that differs from HEAD. It takes no lock,
    /// so it writes nothing, not even the index. A git status that fails throws an InvalidDataException.
    /// </summary>
    public IReadOnlyList<string> Changed()
    {
        // Porcelain v1 with -z: "XY path" entries, and a renamed or copied file adds its old path as the entry after.
        var status = Read("status", "--porcelain", "-z", "--untracked-files=all");
        var entries = status.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var changed = new List<string>();
        for (var at = 0; at < entries.Length; at++)
        {
            changed.Add(entries[at][3..]);
            if (entries[at][0] is 'R' or 'C')
            {
                at++;
            }
        }

        return changed;
    }

    /// <summary>
    /// The commit where HEAD left the default branch: its merge-base with the branch origin/HEAD names, the local one
    /// before the remote one, else with master, else with main. Null when Root is no git repo, HEAD has no commit, or
    /// none of these branches shares a commit with HEAD.
    /// </summary>
    public string? BranchPoint()
    {
        var branches = new List<string>();
        if (Ask("symbolic-ref", "--quiet", "--short", "refs/remotes/origin/HEAD")?.Trim() is { Length: > 0 } remote)
        {
            var local = remote.StartsWith("origin/", StringComparison.Ordinal) ? remote["origin/".Length..] : remote;
            branches.Add(local);
            branches.Add(remote);
        }

        branches.Add("master");
        branches.Add("main");
        foreach (var branch in branches)
        {
            if (Ask("merge-base", "HEAD", branch)?.Trim() is { Length: > 0 } commit)
            {
                return commit;
            }
        }

        return null;
    }

    /// <summary>
    /// The lines the working tree changes since the given commit, by the path of each file relative to the repo, as the
    /// working tree numbers them. A tracked file's lines come from the diff, staged and unstaged work included, and
    /// lines removed from between two lines are the empty range between them. Every line of an untracked file is
    /// changed. A deleted file has no lines left, so it is not among them. It takes no lock, so it writes nothing. A git
    /// diff or ls-files that fails throws an InvalidDataException.
    /// </summary>
    public IReadOnlyDictionary<string, List<LineRange>> ChangedLinesSince(string commit)
    {
        var changed = new Dictionary<string, List<LineRange>>(StringComparer.Ordinal);
        var diff = Read("diff", "--no-relative", "--no-renames", "--no-ext-diff", "--no-color", "--src-prefix=a/", "--dst-prefix=b/", "-U0", commit, "--");

        // A file's hunks follow its "+++ b/<path>" line; a deleted file's is "+++ /dev/null" and has none worth reading.
        List<LineRange>? ranges = null;
        foreach (var line in diff.Split('\n'))
        {
            if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                ranges = null;
                if (line.StartsWith("+++ b/", StringComparison.Ordinal))
                {
                    ranges = [];
                    changed[line["+++ b/".Length..].TrimEnd('\t')] = ranges;
                }

                continue;
            }

            if (ranges is not null && line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                ranges.Add(LineRange.OfHunk(line));
            }
        }

        var others = Read("ls-files", "--others", "--exclude-standard", "-z");
        var untracked = others.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in untracked)
        {
            changed[path] = [LineRange.Whole];
        }

        return changed;
    }

    // git's stdout, or null when git cannot run or exits with anything but 0: git's answer no.
    private string? Ask(params string[] arguments)
    {
        var finished = Run(arguments);
        if (finished is null || finished.ExitCode != 0)
        {
            return null;
        }

        return finished.Output;
    }

    // The stdout of the given git command with its options. It takes no lock, so it writes nothing, and it names paths
    // unquoted. A git that cannot run or exits with anything but 0 throws, naming the command and carrying git's stderr
    // on one line.
    private string Read(string command, params string[] options)
    {
        string[] arguments = ["--no-optional-locks", "-c", "core.quotePath=false", command, .. options];
        var finished = Run(arguments);
        if (finished is null)
        {
            throw new InvalidDataException($"git {command} cannot run: git is not on PATH");
        }

        if (finished.ExitCode != 0)
        {
            var stderr = string.Join("; ", finished.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            throw new InvalidDataException($"git {command} failed with exit {finished.ExitCode}: {stderr}");
        }

        return finished.Output;
    }

    // How git finished, or null when git cannot run. A git that runs longer than Timeout is killed and throws.
    private Finished? Run(string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(start)!;
        }
        catch (Win32Exception)
        {
            return null;
        }

        using (process)
        {
            // Both streams are read while git runs, so neither fills its pipe and blocks git.
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(Timeout))
            {
                process.Kill(entireProcessTree: true);
                throw new InvalidDataException($"git {string.Join(' ', arguments)} did not finish within {Timeout.TotalSeconds} seconds and was killed");
            }

            return new Finished(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
        }
    }

    // A git command that ran to its end: its exit code, stdout and stderr.
    private sealed record Finished(int ExitCode, string Output, string Error);
}
