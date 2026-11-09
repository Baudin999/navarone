namespace Navarone.Engine.Cache;

/// <summary>The line of a repo's .gitignore that keeps the facts cache out of git, which `navarone init` adds.</summary>
public static class CacheIgnore
{
    /// <summary>The line as init writes it.</summary>
    public const string Line = $"{FactsCache.Folder}/";

    // The lines that ignore the cache folder at the repo root already, so init adds none of its own.
    private static readonly string[] Ignoring = [FactsCache.Folder, Line, $"/{FactsCache.Folder}", $"/{Line}"];

    /// <summary>Where the .gitignore lies in the repo at the given root.</summary>
    public static string PathIn(string repoRoot) => Path.Combine(repoRoot, ".gitignore");

    /// <summary>
    /// Adds the line to the .gitignore at the repo root, creating the file when there is none. Returns whether it added
    /// it: a .gitignore that ignores the folder already is left as it is. A file that cannot be written throws an
    /// InvalidDataException.
    /// </summary>
    public static bool Add(string repoRoot)
    {
        var path = PathIn(repoRoot);
        var text = "";
        if (File.Exists(path))
        {
            text = File.ReadAllText(path);
        }

        var lines = text.Split('\n').Select(line => line.Trim());
        if (lines.Any(line => Ignoring.Contains(line)))
        {
            return false;
        }

        var end = "\n";
        if (text.Contains("\r\n", StringComparison.Ordinal))
        {
            end = "\r\n";
        }

        if (text.Length > 0 && !text.EndsWith('\n'))
        {
            text += end;
        }

        AtomicFile.WriteOrRefuse(path, $"{text}{Line}{end}");
        return true;
    }
}
