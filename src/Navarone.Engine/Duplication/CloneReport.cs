using System.Globalization;
using Navarone.Engine.Lenses;

namespace Navarone.Engine.Duplication;

/// <summary>
/// Writes clone pairs as markdown: per pair its similarity, its size and its share of same names, both locations as
/// file and line range with the method each lies in, and both pieces of code, each line that holds a token the other
/// piece does not match marked with `~`.
/// </summary>
public static class CloneReport
{
    /// <summary>
    /// Writes the pairs as markdown to the given writer, one pair at a time, under a line that states the floor and how
    /// many pairs it lets through; the code blocks are in the given language.
    /// </summary>
    public static async Task WriteAsync(TextWriter writer, IReadOnlyList<ClonePair> pairs, CloneFloor floor, string language)
    {
        var header = new Markdown(language);
        header.Line(string.Create(
            CultureInfo.InvariantCulture,
            $"{pairs.Count} clone pairs at similarity {floor.Similarity:0.00} or more and {floor.Tokens} tokens or more. A line marked ~ holds a token the other piece does not match; identifiers and literals match whatever they are named. Same names is the share of the matched identifiers spelled the same on both sides."));
        await writer.WriteAsync(header.ToString()).ConfigureAwait(false);
        for (var index = 0; index < pairs.Count; index++)
        {
            var pair = pairs[index];
            var markdown = new Markdown(language);
            markdown.Heading(2, string.Create(CultureInfo.InvariantCulture, $"{index + 1}. similarity {pair.Similarity:0.00}, {pair.Tokens} tokens, same names {pair.SameNames:0.00}"));
            Piece(markdown, pair.First);
            Piece(markdown, pair.Second);
            await writer.WriteAsync(markdown.ToString()).ConfigureAwait(false);
        }
    }

    private static void Piece(Markdown markdown, ClonePiece piece)
    {
        markdown.Line(string.Create(CultureInfo.InvariantCulture, $"{piece.Symbol.Value}, lines {piece.FirstLine} to {piece.LastLine}"));
        var code = string.Join('\n', piece.Lines.Select(line => (line.Differs ? "~ " : "  ") + line.Text));
        markdown.Code(piece.Path, piece.FirstLine, code);
    }
}
