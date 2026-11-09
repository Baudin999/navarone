using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Navarone.Engine.Facts;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>Where code is written: a file's path, a declaration's location and its lines as written.</summary>
internal static class SourcePlaces
{
    /// <summary>The path of a file relative to the solution's directory, or its name when either is unknown.</summary>
    public static string PathOf(string? directory, string? file, string name)
    {
        if (directory is null)
        {
            return name;
        }

        if (string.IsNullOrEmpty(file))
        {
            return name;
        }

        return Path.GetRelativePath(directory, file);
    }

    /// <summary>
    /// The whole lines of the file from the first node's first attribute or modifier, after its leading comments, to
    /// the end of the last node, as written.
    /// </summary>
    public static Written WrittenOf(SyntaxNode first, SyntaxNode last)
    {
        var text = first.SyntaxTree.GetText();
        var from = text.Lines.GetLineFromPosition(first.SpanStart);
        var to = text.Lines.GetLineFromPosition(last.Span.End);
        var span = TextSpan.FromBounds(from.Start, to.End);
        var written = text.ToString(span);
        return new Written(from.LineNumber + 1, written);
    }

    /// <summary>Where the symbol's name stands in the given declaration, or where the declaration starts when it names none there.</summary>
    public static SourceLocation LocationOf(ISymbol symbol, SyntaxNode declaration, string path)
    {
        var location = declaration.GetLocation();
        foreach (var named in symbol.Locations)
        {
            if (named.SourceTree == declaration.SyntaxTree && declaration.Span.Contains(named.SourceSpan))
            {
                location = named;
                break;
            }
        }

        var start = location.GetLineSpan().StartLinePosition;
        return new SourceLocation(path, start.Line + 1, start.Character + 1);
    }
}
