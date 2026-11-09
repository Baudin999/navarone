using System.Globalization;
using System.Text;

namespace Navarone.Engine.Lenses;

/// <summary>
/// Writes what a lens shows as markdown: headings, lines of text and code blocks in the provider's language, each code
/// block under its path and start line.
/// </summary>
internal sealed class Markdown(string language)
{
    // Markdown has six levels of heading; a deeper one stays at six.
    private const int DeepestHeading = 6;

    private readonly StringBuilder text = new();

    /// <summary>A heading at the given level, from 1.</summary>
    public void Heading(int level, string title)
    {
        text.Append('#', Math.Min(level, DeepestHeading)).Append(' ').Append(title).Append("\n\n");
    }

    /// <summary>A paragraph of one line.</summary>
    public void Line(string line)
    {
        text.Append(line).Append("\n\n");
    }

    /// <summary>
    /// A code block whose first line is the given line of the file at the given path. Its fence is longer than any run
    /// of backticks in the code, so the code cannot close it.
    /// </summary>
    public void Code(string path, int line, string code)
    {
        var fence = new string('`', Math.Max(3, LongestBacktickRun(code) + 1));
        text.Append(string.Create(CultureInfo.InvariantCulture, $"`{path}:{line}`")).Append("\n\n");
        text.Append(fence).Append(language).Append('\n');
        text.Append(code.TrimEnd()).Append('\n');
        text.Append(fence).Append("\n\n");
    }

    /// <summary>Everything written so far.</summary>
    public override string ToString() => text.ToString();

    private static int LongestBacktickRun(string code)
    {
        var longest = 0;
        var run = 0;
        foreach (var character in code)
        {
            if (character != '`')
            {
                run = 0;
                continue;
            }

            run++;
            longest = Math.Max(longest, run);
        }

        return longest;
    }
}
