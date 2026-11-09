namespace Navarone.Engine;

/// <summary>
/// Which signals `navarone` shows: every signal in the repo, or only those whose code the current change touches. It
/// decides only what is shown; every problem is still found, granted an id and recorded.
/// </summary>
public sealed class Coverage
{
    // The changed lines by a file's path relative to a codebase's directory, as a fact names it; null covers all code.
    private readonly Dictionary<string, List<LineRange>>? changed;

    private Coverage(Dictionary<string, List<LineRange>>? changed) => this.changed = changed;

    /// <summary>Every signal in the repo, for `navarone --full-coverage`.</summary>
    public static Coverage Full { get; } = new(null);

    /// <summary>
    /// The signals in the code the change touches: the diff between the working tree of the repo at the given root and
    /// the commit where HEAD left the default branch, uncommitted and untracked work included. A path a fact names is
    /// relative to its codebase's directory, so each changed file is named from the directory of each of the given
    /// codebases. A repo with no such commit, outside git or with no default branch, throws an InvalidDataException, and
    /// so does a git that fails or times out while it reads the change.
    /// </summary>
    public static Coverage Touched(string repoRoot, IReadOnlyList<string> codebases)
    {
        var git = new Git(repoRoot);
        if (git.BranchPoint() is not { } branchPoint)
        {
            throw new InvalidDataException("no change to show: HEAD shares no commit with the default branch, the one origin/HEAD names, master or main; navarone --full-coverage shows every signal");
        }

        var directories = codebases.Select(codebase => Path.GetDirectoryName(Path.GetFullPath(codebase))!).Distinct(StringComparer.Ordinal).ToList();
        var changed = new Dictionary<string, List<LineRange>>(StringComparer.Ordinal);
        foreach (var (path, ranges) in git.ChangedLinesSince(branchPoint))
        {
            var file = Path.GetFullPath(Path.Combine(repoRoot, path));
            foreach (var directory in directories)
            {
                // Two codebases can name different files by one relative path; the path then holds both files' ranges.
                var named = Path.GetRelativePath(directory, file);
                if (!changed.TryGetValue(named, out var known))
                {
                    known = [];
                    changed[named] = known;
                }

                known.AddRange(ranges);
            }
        }

        return new Coverage(changed);
    }

    /// <summary>Whether this coverage shows a signal whose code spans lines from to through of the file at path.</summary>
    internal bool Covers(string path, int from, int through)
    {
        if (changed is null)
        {
            return true;
        }

        return changed.TryGetValue(path, out var ranges) && ranges.Exists(range => range.Touches(from, through));
    }
}
