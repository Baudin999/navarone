using System.Globalization;

namespace Navarone.Engine;

/// <summary>
/// Lines First to Last of a file, from 1, that a change touches. Last is First minus one for lines removed from between
/// line Last and line First: the empty range between them.
/// </summary>
internal readonly record struct LineRange(int First, int Last)
{
    /// <summary>Every line of a file.</summary>
    public static LineRange Whole { get; } = new(1, int.MaxValue);

    /// <summary>
    /// The lines of the new side of a diff hunk, from its header, `@@ -a,b +c,d @@`. A count of 0 means lines were
    /// only removed, after line c; a count left out means 1.
    /// </summary>
    public static LineRange OfHunk(string header)
    {
        var added = header.Split(' ')[2][1..].Split(',');
        var start = int.Parse(added[0], CultureInfo.InvariantCulture);
        var count = 1;
        if (added.Length > 1)
        {
            count = int.Parse(added[1], CultureInfo.InvariantCulture);
        }

        if (count == 0)
        {
            return new LineRange(start + 1, start);
        }

        return new LineRange(start, start + count - 1);
    }

    /// <summary>
    /// Whether the change touches lines from to through of the file: a changed line among them, or lines removed from
    /// between two of them.
    /// </summary>
    public bool Touches(int from, int through) => First <= through && Last >= from;
}
