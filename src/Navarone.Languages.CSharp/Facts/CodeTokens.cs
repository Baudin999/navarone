using System.Collections.Frozen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Navarone.Engine.Facts;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>
/// The tokens of a method's bodies, normalised so a renamed copy reads the same as its original. Every identifier is
/// `id`, contextual keywords such as `var` included, since C# lexes them as identifiers. Every literal is `lit`: a
/// number, a character, a string, an interpolated string's text, a raw string, `true` and `false`. Every other token,
/// a keyword, `null` or punctuation, stays as written. Comments and whitespace are trivia and never tokens.
/// </summary>
internal static class CodeTokens
{
    private static readonly FrozenSet<SyntaxKind> Literals = FrozenSet.Create(
        SyntaxKind.NumericLiteralToken,
        SyntaxKind.CharacterLiteralToken,
        SyntaxKind.StringLiteralToken,
        SyntaxKind.Utf8StringLiteralToken,
        SyntaxKind.InterpolatedStringTextToken,
        SyntaxKind.SingleLineRawStringLiteralToken,
        SyntaxKind.MultiLineRawStringLiteralToken,
        SyntaxKind.Utf8SingleLineRawStringLiteralToken,
        SyntaxKind.Utf8MultiLineRawStringLiteralToken,
        SyntaxKind.TrueKeyword,
        SyntaxKind.FalseKeyword);

    /// <summary>The tokens of the given bodies, each with its normalised shape, in the order written, each with the line it starts on.</summary>
    public static List<CodeToken> Of(IReadOnlyList<SyntaxNode> bodies)
    {
        var tokens = new List<CodeToken>();
        foreach (var body in bodies)
        {
            foreach (var token in body.DescendantTokens())
            {
                if (token.IsMissing || token.Span.IsEmpty)
                {
                    continue;
                }

                var line = token.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                tokens.Add(new CodeToken(ShapeOf(token), token.Text, line));
            }
        }

        return tokens;
    }

    private static string ShapeOf(SyntaxToken token)
    {
        var kind = token.Kind();
        if (kind == SyntaxKind.IdentifierToken)
        {
            return CodeToken.Identifier;
        }

        return Literals.Contains(kind) ? CodeToken.Literal : token.Text;
    }
}
