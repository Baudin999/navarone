using System.Security.Cryptography;
using System.Text;
using Navarone.Engine.Configuration;
using Navarone.Engine.Providers;

namespace Navarone.Engine.Cache;

/// <summary>
/// The version of the code whose facts a cache entry holds, as one SHA-256 hash: the full hash of HEAD, the path and
/// contents of every tracked or untracked file that differs from HEAD, the contents of navarone.json, the codebases
/// loaded, and the build of Navarone that makes the facts. A clean tree and a dirty one have different keys, and so do
/// two builds of Navarone.
/// </summary>
internal static class FactsKey
{
    /// <summary>
    /// The key of the given code. Null outside a git repository, in one with no commit yet, and when git status or a
    /// changed file cannot be read: there is no version to key the facts by.
    /// </summary>
    public static string? Of(Code code)
    {
        var git = new Git(code.RepoRoot);
        var commit = git.Head();
        if (commit is null)
        {
            return null;
        }

        try
        {
            return Hash(code, commit, git.Changed());
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // The hash of every part of the key, each with its length, so no two keys run into each other.
    private static string Hash(Code code, string commit, IReadOnlyList<string> changed)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AddText(hash, commit);
        AddText(hash, Build(code.Provider));
        AddFile(hash, NavaroneConfig.PathIn(code.RepoRoot));
        AddNumber(hash, code.Codebases.Count);
        foreach (var path in code.Codebases)
        {
            AddText(hash, path);
        }

        var ordered = changed.Order(StringComparer.Ordinal).ToList();
        AddNumber(hash, ordered.Count);
        foreach (var path in ordered)
        {
            AddText(hash, path);
            AddFile(hash, Path.Combine(code.RepoRoot, path));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    // The build of Navarone that makes the facts: the provider's language, the engine that merges and stores them and
    // the provider that builds them. Each build of an assembly has its own module version id, so no build reads facts
    // another wrote.
    private static string Build(ILanguageProvider provider)
    {
        var engine = typeof(FactsKey).Assembly.ManifestModule.ModuleVersionId;
        var builder = provider.GetType().Assembly.ManifestModule.ModuleVersionId;
        return $"{provider.Language} {engine} {builder}";
    }

    // A file's contents, or a mark that there is no file at the path, such as a deleted one.
    private static void AddFile(IncrementalHash hash, string path)
    {
        if (!File.Exists(path))
        {
            AddNumber(hash, -1);
            return;
        }

        var contents = File.ReadAllBytes(path);
        AddNumber(hash, contents.Length);
        hash.AppendData(contents);
    }

    private static void AddText(IncrementalHash hash, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        AddNumber(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AddNumber(IncrementalHash hash, long number)
    {
        hash.AppendData(BitConverter.GetBytes(number));
    }
}
