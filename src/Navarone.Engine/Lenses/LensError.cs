namespace Navarone.Engine.Lenses;

/// <summary>
/// A lens Navarone cannot show: what is wrong, then the lens with a caret under the column where it is. The CLI prints
/// it as a usage error.
/// </summary>
internal static class LensError
{
    /// <summary>The error at the given column of the given lens, from 1.</summary>
    public static InvalidDataException At(string lens, int column, string what)
    {
        var caret = new string(' ', column - 1);
        return new InvalidDataException($"{what}, at column {column} of the lens\n  {lens}\n  {caret}^");
    }
}
