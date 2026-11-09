using System.Text.Json;
using System.Text.Json.Serialization;

namespace Navarone.Engine.Configuration;

/// <summary>
/// The model of navarone.json at the root of the analysed repo: each check's limit, as
/// <c>{ "limits": { "cyclomatic": 8 } }</c>, and what Navarone ignores, as
/// <c>{ "ignore": { "files": ["src/Generated/**"], "projects": ["*.Tests"] } }</c>. A check the file does not name
/// takes its default. The file may tighten a limit, never loosen it: the agent runs Navarone and could otherwise write
/// its own findings away. An ignore has no such ceiling: the user trusts agents with it and reverts it by hand.
/// </summary>
public sealed record NavaroneConfig(CheckLimits Limits, Ignores Ignores)
{
    /// <summary>The file name the engine reads.</summary>
    public const string FileName = "navarone.json";

    /// <summary>What `navarone init` writes: the test projects ignored, by the .NET names `*.Test` and `*.Tests`.</summary>
    public const string Initial = """
        {
          "ignore": {
            "projects": ["*.Test", "*.Tests"]
          }
        }

        """;

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Reads navarone.json from the root of the analysed repo. A missing file holds the defaults and ignores nothing,
    /// and so does a file without an ignore. A key other than limits and ignore, a limit for a check id Navarone does
    /// not know, a limit above its default, an ignore with a key other than files and projects, a glob that is null or
    /// empty, or a file that is not valid JSON, throws an InvalidDataException, so a typo never leaves a check at its
    /// default or a glob unread unnoticed and no file raises a limit.
    /// </summary>
    public static NavaroneConfig Load(string repoRoot)
    {
        var path = PathIn(repoRoot);
        if (!File.Exists(path))
        {
            return new NavaroneConfig(CheckLimits.Defaults, Ignores.Nothing(repoRoot));
        }

        var file = Read(path);
        var limits = new Dictionary<string, int>(CheckLimits.Defaults.Limits, StringComparer.Ordinal);
        foreach (var (checkId, limit) in file.Limits ?? [])
        {
            if (!limits.TryGetValue(checkId, out var standard))
            {
                throw new InvalidDataException($"{FileName} sets a limit for '{checkId}', which is not a check.");
            }

            if (limit > standard)
            {
                throw new InvalidDataException($"{FileName} raises the limit of '{checkId}' to {limit}, above its default of {standard}; a limit may only be lowered.");
            }

            limits[checkId] = limit;
        }

        var ignore = file.Ignore ?? IgnoreSection.Empty;
        return new NavaroneConfig(new CheckLimits(limits), ignore.Of(repoRoot));
    }

    /// <summary>Where navarone.json lies in the repo at the given root.</summary>
    public static string PathIn(string repoRoot) => Path.Combine(repoRoot, FileName);

    /// <summary>
    /// Writes the initial navarone.json at the root of the repo, unless one is there. Returns whether it wrote it, so a
    /// navarone.json that exists is never touched. A file that cannot be written throws an InvalidDataException.
    /// </summary>
    public static bool Init(string repoRoot)
    {
        var path = PathIn(repoRoot);
        if (File.Exists(path))
        {
            return false;
        }

        AtomicFile.WriteOrRefuse(path, Initial);
        return true;
    }

    // The file as read; a file that holds only null holds nothing.
    private static ConfigFile Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<ConfigFile>(File.ReadAllText(path), Options) ?? ConfigFile.Empty;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{FileName} is not valid: {exception.Message}", exception);
        }
    }

    // A top-level key other than limits and ignore is refused as the file is read, so a misspelled section such as
    // "ignores" never leaves its globs unread unnoticed.
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record ConfigFile(Dictionary<string, int>? Limits, IgnoreSection? Ignore)
    {
        public static ConfigFile Empty { get; } = new(null, null);
    }
}
