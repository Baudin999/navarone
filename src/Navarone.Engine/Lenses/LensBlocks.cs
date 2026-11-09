using System.Globalization;
using System.Text.Json.Nodes;

namespace Navarone.Engine.Lenses;

/// <summary>
/// A block of a lens page, which writes itself both ways: into markdown, and as a block of brain's window, a text block
/// or a code block.
/// </summary>
internal abstract record LensBlock
{
    /// <summary>Writes the block into the given markdown.</summary>
    public abstract void Write(Markdown markdown);

    /// <summary>The block as a block of brain's window.</summary>
    public abstract JsonObject Json();

    /// <summary>A code block of brain's window: the code, whose first line is the given line of the file at the given path.</summary>
    protected static JsonObject CodeBlock(string path, int line, string code, JsonArray marks) => new()
    {
        ["type"] = "code",
        ["path"] = path,
        ["startLine"] = line,
        ["code"] = code,
        ["marks"] = marks,
    };

    /// <summary>
    /// A mark on the given line of the file, whose text is the given text: from its first character to its last that is
    /// not white space, in columns of UTF-16 code units from 1, the end one past the span. It weighs 0, since a lens
    /// counts nothing.
    /// </summary>
    protected static JsonObject Mark(int line, string text, string label, string note)
    {
        var first = text.Length - text.TrimStart().Length;
        var last = text.TrimEnd().Length;
        return new JsonObject
        {
            ["start"] = Position(line, first + 1),
            ["end"] = Position(line, last + 1),
            ["label"] = label,
            ["weight"] = 0,
            ["note"] = note,
        };
    }

    private static JsonObject Position(int line, int col) => new() { ["line"] = line, ["col"] = col };
}

/// <summary>A paragraph of one line, a text block in brain's window.</summary>
internal sealed record LensText(string Text) : LensBlock
{
    public override void Write(Markdown markdown) => markdown.Line(Text);

    public override JsonObject Json() => new() { ["type"] = "text", ["text"] = Text };
}

/// <summary>
/// A piece of code whose first line is the given line of the file at the given path, with its marks, each on a line
/// of the code.
/// </summary>
internal sealed record LensCode(string Path, int Line, string Code, IReadOnlyList<LensMark> Marks) : LensBlock
{
    public override void Write(Markdown markdown) => markdown.Code(Path, Line, Code);

    public override JsonObject Json()
    {
        var shown = Code.TrimEnd();
        var lines = shown.Split('\n');
        var marks = new JsonArray();
        foreach (var mark in Marks)
        {
            var json = Mark(mark.Line, lines[mark.Line - Line], mark.Label, mark.Note);
            json["queries"] = new JsonArray([.. mark.Queries.Select(query => new JsonObject { ["label"] = query.Label, ["query"] = query.Query })]);
            marks.Add(json);
        }

        return CodeBlock(Path, Line, shown, marks);
    }
}

/// <summary>
/// Code the page showed above, at the given path and line, in the section with the given id. In brain's window it is
/// its first line, marked with that section, so a click goes there.
/// </summary>
internal sealed record LensShownAbove(string Path, int Line, string FirstLine, string Section) : LensBlock
{
    public override void Write(Markdown markdown) => markdown.Line($"Shown above at `{Path}:{Line}`.");

    public override JsonObject Json()
    {
        var mark = Mark(Line, FirstLine, "shown above", string.Create(CultureInfo.InvariantCulture, $"Shown above at {Path}:{Line}."));
        mark["section"] = Section;
        return CodeBlock(Path, Line, FirstLine, [mark]);
    }
}

/// <summary>
/// A line of code that opens other lenses, one per query, in the order they are offered. Calls are known by line only,
/// so a line with several calls carries a query for each.
/// </summary>
internal sealed record LensMark(int Line, string Label, string Note, IReadOnlyList<LensQuery> Queries);

/// <summary>A lens to open, as its words, labelled with what it opens.</summary>
internal sealed record LensQuery(string Label, string Query);
