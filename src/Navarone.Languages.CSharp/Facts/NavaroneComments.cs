using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Navarone.Languages.CSharp.Facts;

/// <summary>The `// navarone: &lt;id&gt;` comments: the ids they hold, and a declaration's code without them.</summary>
internal static partial class NavaroneComments
{
    /// <summary>The ids in the given trivia, as written.</summary>
    public static List<string> Ids(IEnumerable<SyntaxTrivia> trivia) =>
    [
        .. trivia
            .Where(item => item.IsKind(SyntaxKind.SingleLineCommentTrivia))
            .Select(item => Comment().Match(item.ToString()))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value),
    ];

    /// <summary>
    /// The ids written at a declaration: in its leading comments, and between or after its attributes, up to its
    /// signature. A comment after [Theory] or [HttpGet] is at the method as much as one above it.
    /// </summary>
    public static List<string> IdsAt(SyntaxNode declaration)
    {
        var attributes = declaration switch
        {
            MemberDeclarationSyntax member => member.AttributeLists,
            AccessorDeclarationSyntax accessor => accessor.AttributeLists,
            _ => default,
        };
        var signature = attributes is [.., var last] ? last.GetLastToken().GetNextToken().SpanStart : declaration.SpanStart;
        return Ids(declaration.DescendantTrivia().Where(item => item.Span.End <= signature));
    }

    /// <summary>The ids written anywhere in the declaration, its leading comments included, since descendant trivia starts with them, and the comment after its closing brace. That one is outside the code, so it never reaches the fingerprint.</summary>
    public static List<string> IdsIn(SyntaxNode declaration) => Ids(declaration.DescendantTrivia());

    /// <summary>
    /// The declaration as written, without its leading trivia and without any navarone comment inside it. A comment
    /// alone on its line leaves no blank line behind, and one after code leaves no trailing space.
    /// </summary>
    public static string CodeWithout(SyntaxNode declaration)
    {
        var text = declaration.ToString();
        var start = declaration.SpanStart;
        var code = new StringBuilder(text);
        var comments = declaration.DescendantTrivia()
            .Where(item => declaration.Span.Contains(item.Span) && item.IsKind(SyntaxKind.SingleLineCommentTrivia) && Comment().IsMatch(item.ToString()))
            .OrderByDescending(item => item.SpanStart);
        foreach (var comment in comments)
        {
            var from = text.AsSpan(0, comment.SpanStart - start).TrimEnd(" \t").Length;
            var to = comment.Span.End - start;
            if (from == 0 || text[from - 1] == '\n')
            {
                var lineEnd = text.IndexOf('\n', to);
                to = lineEnd < 0 ? text.Length : lineEnd + 1;
            }

            code.Remove(from, to - from);
        }

        return code.ToString();
    }

    [GeneratedRegex(@"^//\s*navarone:\s*(?<id>\S+)\s*$")]
    private static partial Regex Comment();
}
