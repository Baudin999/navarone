using System.IO.Enumeration;
using Navarone.Engine.Providers;

namespace Navarone.Cli;

/// <summary>Where the CLI finds the repo it runs in and the codebases it analyses.</summary>
internal static class FileLocations
{
    /// <summary>
    /// The root of the repo `navarone` runs in: the nearest directory up from the given one that holds .git, or
    /// the given directory when none does. reasons.json and navarone.json live here.
    /// </summary>
    public static string RepoRoot(string current)
    {
        for (var directory = new DirectoryInfo(current); directory is not null; directory = directory.Parent)
        {
            if (Path.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }
        }

        return current;
    }

    /// <summary>
    /// Every file anywhere under the repo root the provider can load, such as each .sln and .slnx, in path order.
    /// bin and obj directories, hidden ones, symbolic links and what the user cannot read are skipped. Navarone
    /// analyses each one; none is a usage error.
    /// </summary>
    public static IReadOnlyList<string> Codebases(string repoRoot, ILanguageProvider provider)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.ReparsePoint,
        };
        var files = new FileSystemEnumerable<string>(repoRoot, (ref FileSystemEntry entry) => entry.ToFullPath(), options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory,
            ShouldRecursePredicate = (ref FileSystemEntry entry) => entry.FileName is not ("bin" or "obj"),
        };
        return [.. files.Where(provider.CanLoad).Order(StringComparer.Ordinal)];
    }
}
