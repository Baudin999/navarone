using System.Text.Json.Nodes;

namespace Navarone.Engine.Lenses;

/// <summary>
/// What a lens shows, built once as sections of blocks and marks, and written two ways from it: as markdown for an
/// agent and as JSON for brain's window, so the two never drift. A section is a heading with its blocks and the sections
/// under it, to any depth; brain's window draws a section open when Open says so. A mark is a line of code that opens
/// another lens: a call carries a lens per method it calls on that line, and code shown above is a mark that points at
/// the section that shows it.
/// </summary>
public sealed class LensPage
{
    private readonly string language;
    private readonly IReadOnlyList<LensSection> sections;

    internal LensPage(string language, IReadOnlyList<LensSection> sections)
    {
        this.language = language;
        this.sections = sections;
    }

    /// <summary>
    /// The page as markdown: a heading per section, one level deeper than the section it sits under, then its blocks,
    /// then the sections under it. Marks are not written.
    /// </summary>
    public string Markdown()
    {
        var markdown = new Markdown(language);
        foreach (var section in sections)
        {
            Write(markdown, section, 1);
        }

        return markdown.ToString();
    }

    private static void Write(Markdown markdown, LensSection section, int level)
    {
        markdown.Heading(level, section.Title);
        foreach (var block in section.Blocks)
        {
            block.Write(markdown);
        }

        foreach (var child in section.Sections)
        {
            Write(markdown, child, level + 1);
        }
    }

    /// <summary>
    /// The page as the JSON brain's window draws: the given commit, the full hash of the code shown or null, and each
    /// section with its id, title, whether it is open, its blocks in brain's block types, text and code, and the
    /// sections under it when it has any.
    /// </summary>
    public JsonObject Json(string? commit) => new()
    {
        ["commit"] = commit,
        ["sections"] = new JsonArray([.. sections.Select(Json)]),
    };

    private static JsonNode Json(LensSection section)
    {
        var json = new JsonObject
        {
            ["id"] = section.Id,
            ["title"] = section.Title,
            ["open"] = section.Open,
            ["blocks"] = new JsonArray([.. section.Blocks.Select(block => block.Json())]),
        };
        if (section.Sections.Count > 0)
        {
            json["sections"] = new JsonArray([.. section.Sections.Select(Json)]);
        }

        return json;
    }
}

/// <summary>
/// A heading of a lens page with its blocks and the sections under it. Id is unique on the page; Open is true for the
/// subject.
/// </summary>
internal sealed record LensSection(string Id, string Title, bool Open)
{
    public List<LensBlock> Blocks { get; } = [];

    public List<LensSection> Sections { get; } = [];
}
