namespace Navarone.Engine.Configuration;

/// <summary>
/// Each check's limit, keyed by check id. A check raises a finding only above its limit.
/// A check missing from navarone.json takes its default.
/// </summary>
public sealed record CheckLimits(IReadOnlyDictionary<string, int> Limits)
{
    /// <summary>The defaults from SPEC.md's table.</summary>
    public static CheckLimits Defaults { get; } = new(new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["cyclomatic"] = 10,
        ["cognitive"] = 15,
        ["lcom4"] = 1,
        ["control-coupling"] = 0,
        ["split-complexity"] = 10,
    });

    /// <summary>The limit of the check with the given id, or its default when these limits do not name it.</summary>
    public int For(string checkId) =>
        Limits.TryGetValue(checkId, out var limit) ? limit : Defaults.Limits[checkId];
}
