namespace Navarone.Engine.Lenses;

/// <summary>
/// The line that opens every lens output: the version of the code the lens shows. What anyone decides about code holds
/// only for the version it was decided on.
/// </summary>
public static class LensVersion
{
    /// <summary>
    /// The full hash of HEAD in the repo at the given root, or null outside a git repository or in one with no commit
    /// yet.
    /// </summary>
    public static string? Commit(string repoRoot) => new Git(repoRoot).Head();

    /// <summary>
    /// The paths, relative to the repo, of every file that differs from HEAD, none outside a git repository or with no
    /// commit yet. A git status that fails or times out throws an InvalidDataException.
    /// </summary>
    public static IReadOnlyList<string> Changed(string repoRoot)
    {
        var git = new Git(repoRoot);
        if (git.Head() is null)
        {
            return [];
        }

        return git.Changed();
    }

    /// <summary>
    /// The version of the code in the repo at the given root as markdown, ending in a blank line. A git status that fails
    /// or times out throws an InvalidDataException.
    /// </summary>
    public static string Of(string repoRoot)
    {
        var git = new Git(repoRoot);
        if (git.Head() is not { } commit)
        {
            return git.IsRepo()
                ? "The code is in a git repository with no commit yet, so it has no version.\n\n"
                : "The code is not in a git repository, so it has no version.\n\n";
        }

        var changed = git.Changed();
        return changed.Count == 0
            ? $"At commit {commit}.\n\n"
            : $"At commit {commit}, with uncommitted changes, so the code shown is the working tree, not the commit. Changed files:\n\n{string.Concat(changed.Select(path => $"- `{path}`\n"))}\n";
    }
}
