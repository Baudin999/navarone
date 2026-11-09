using System.Text.RegularExpressions;

namespace Navarone.Engine.Configuration;

/// <summary>
/// What navarone.json ignores in the repo at its root, as
/// <c>{ "ignore": { "files": ["src/Generated/**"], "projects": ["*.Tests"] } }</c>. A file glob matches a file's whole
/// path relative to the root, with / between directories. A project glob matches a project's name. In a glob, **
/// matches any depth, so **/ also matches no directory at all, * matches any characters but /, and ? one character
/// but /. Every other character matches itself. A provider reads no fact from ignored code, so it is gone from all
/// of Navarone.
/// </summary>
public sealed class Ignores
{
    private readonly string root;
    private readonly List<Regex> files;
    private readonly List<Regex> projects;

    /// <summary>The given file and project globs in the repo at the given root.</summary>
    public Ignores(string root, IReadOnlyList<string> files, IReadOnlyList<string> projects)
    {
        this.root = root;
        this.files = [.. files.Select(Pattern)];
        this.projects = [.. projects.Select(Pattern)];
    }

    /// <summary>Nothing ignored in the repo at the given root.</summary>
    public static Ignores Nothing(string root) => new(root, [], []);

    /// <summary>
    /// Whether a file glob matches the file at the given absolute path. A file without a path, or outside the root, is
    /// never ignored.
    /// </summary>
    public bool IgnoresFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return false;
        }

        return Match(files, relative);
    }

    /// <summary>Whether a project glob matches the project with the given name.</summary>
    public bool IgnoresProject(string name) => Match(projects, name);

    // Whether any of the globs matches the whole of the given text.
    private static bool Match(List<Regex> globs, string text) => globs.Exists(glob => glob.IsMatch(text));

    // The glob as a regex over the whole text. Escaping turns each * into \* and each ? into \?, so only the glob's own
    // wildcards are replaced, ** before *.
    private static Regex Pattern(string glob)
    {
        var pattern = Regex.Escape(glob)
            .Replace(@"\*\*/", "(?:.*/)?", StringComparison.Ordinal)
            .Replace(@"\*\*", ".*", StringComparison.Ordinal)
            .Replace(@"\*", "[^/]*", StringComparison.Ordinal)
            .Replace(@"\?", "[^/]", StringComparison.Ordinal);
        return new Regex($"^{pattern}$", RegexOptions.CultureInvariant);
    }
}
