using System.Text.Json;
using Navarone.Engine.Facts;

namespace Navarone.Engine.Cache;

/// <summary>
/// The merged facts of recent versions of the code, one entry per FactsKey in .navarone at the repo root, so a command
/// that reads the same version of the code again reads its facts from disk instead of loading every codebase. The
/// folder holds at most Kept entries. It never fails a command: an entry that cannot be read is a miss, and an entry
/// that cannot be written is left out.
/// </summary>
internal sealed class FactsCache
{
    /// <summary>The folder at the repo root that holds the entries, which init adds to the repo's .gitignore.</summary>
    public const string Folder = ".navarone";

    // How many entries the folder holds at most.
    private const int Kept = 10;

    private readonly string folder;
    private readonly string entry;

    private FactsCache(string folder, string key)
    {
        this.folder = folder;
        entry = Path.Combine(folder, $"{key}.json");
    }

    /// <summary>The cache entry of the given code, or null when the code has no version to key it by, as FactsKey says.</summary>
    public static FactsCache? For(Code code)
    {
        var key = FactsKey.Of(code);
        if (key is null)
        {
            return null;
        }

        return new FactsCache(Path.Combine(code.RepoRoot, Folder), key);
    }

    /// <summary>The facts the entry holds, or null when there is none or it cannot be read, corrupt, partial or of another shape.</summary>
    public CodebaseFacts? Read()
    {
        try
        {
            using var json = File.OpenRead(entry);
            return FactsJson.Read(json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the facts as the entry, whole or not at all, after deleting the oldest other entries so at most Kept
    /// remain once it is written. The folder ignores itself in git, so an entry never shows as a change. Facts that
    /// cannot be written, or a folder that cannot be, leave the entry out.
    /// </summary>
    public void Write(CodebaseFacts facts)
    {
        try
        {
            var json = FactsJson.Write(facts);
            Directory.CreateDirectory(folder);
            IgnoreItself();
            Prune();
            AtomicFile.Write(entry, json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            // The cache is a shortcut: facts it cannot keep are built again by the next command.
        }
    }

    // A .gitignore in the folder that ignores everything in it, itself included, so git status never lists an entry and
    // an entry never changes the key of the code, even in a repo whose own .gitignore does not name the folder.
    private void IgnoreItself()
    {
        var ignore = Path.Combine(folder, ".gitignore");
        if (!File.Exists(ignore))
        {
            AtomicFile.Write(ignore, "*\n");
        }
    }

    // Deletes the oldest entries other than this one until one more fits within Kept.
    private void Prune()
    {
        var own = Path.GetFullPath(entry);
        var others = new DirectoryInfo(folder)
            .GetFiles("*.json")
            .Where(file => file.FullName != own)
            .OrderBy(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.Name, StringComparer.Ordinal)
            .ToList();
        var surplus = others.Count - (Kept - 1);
        for (var at = 0; at < surplus; at++)
        {
            others[at].Delete();
        }
    }
}
