namespace Navarone.Engine;

/// <summary>Turns a fact's path, relative to its codebase's directory, into the path relative to the repo a reader names.</summary>
internal static class RepoPaths
{
    /// <summary>
    /// The repo-relative path of the given fact's path: the one of the codebases' directories under which the file
    /// exists. With none, such as a file of a subject that is gone, the path as it is.
    /// </summary>
    public static string Of(string repoRoot, IReadOnlyList<string> codebases, string path)
    {
        var root = Path.GetFullPath(repoRoot);
        foreach (var codebase in codebases)
        {
            // A codebase is a file under the repo root, so its path has a directory.
            var directory = Path.GetDirectoryName(Path.GetFullPath(codebase))!;
            var file = Path.GetFullPath(Path.Combine(directory, path));
            if (File.Exists(file))
            {
                return Path.GetRelativePath(root, file).Replace('\\', '/');
            }
        }

        return path;
    }
}
