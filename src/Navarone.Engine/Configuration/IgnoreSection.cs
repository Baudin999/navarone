using System.Text.Json.Serialization;

namespace Navarone.Engine.Configuration;

/// <summary>
/// The ignore section of navarone.json as read, before it is checked. A key other than files and projects is refused
/// as the file is read, so a misspelled list never ignores nothing unnoticed.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record IgnoreSection(List<string?>? Files, List<string?>? Projects)
{
    /// <summary>A navarone.json without an ignore section.</summary>
    public static IgnoreSection Empty { get; } = new(null, null);

    /// <summary>
    /// What this section ignores in the repo at the given root. A missing list ignores nothing. A glob that is null or
    /// empty throws an InvalidDataException, so a slip in navarone.json is never read as a glob.
    /// </summary>
    public Ignores Of(string repoRoot) => new(repoRoot, Checked(Files), Checked(Projects));

    private static List<string> Checked(List<string?>? globs)
    {
        var read = new List<string>();
        foreach (var glob in globs ?? [])
        {
            if (string.IsNullOrEmpty(glob))
            {
                throw new InvalidDataException($"{NavaroneConfig.FileName} ignores a glob that is null or empty.");
            }

            read.Add(glob);
        }

        return read;
    }
}
