namespace Navarone.Engine.Lenses;

/// <summary>
/// Splits a lens's text into tokens. `:`, `,`, `[` and `]` are a token each, white space separates and is dropped, and
/// every other run of characters is a word. Nothing is quoted. The last token is always the end of the lens.
/// </summary>
internal static class LensLexer
{
    private static readonly Dictionary<char, LensTokenKind> Punctuation = new()
    {
        [':'] = LensTokenKind.Colon,
        [','] = LensTokenKind.Comma,
        ['['] = LensTokenKind.Open,
        [']'] = LensTokenKind.Close,
    };

    /// <summary>The tokens of the given text, in order, without its white space, and then its end.</summary>
    public static List<LensToken> Tokens(string text)
    {
        var tokens = new List<LensToken>();
        var index = 0;
        while (index < text.Length)
        {
            var start = index;
            index++;
            if (char.IsWhiteSpace(text[start]))
            {
                continue;
            }

            if (Punctuation.TryGetValue(text[start], out var kind))
            {
                tokens.Add(new LensToken(kind, text[start..index], start + 1));
                continue;
            }

            while (index < text.Length && IsWordCharacter(text[index]))
            {
                index++;
            }

            tokens.Add(new LensToken(LensTokenKind.Word, text[start..index], start + 1));
        }

        tokens.Add(new LensToken(LensTokenKind.End, "the end of the lens", text.Length + 1));
        return tokens;
    }

    /// <summary>Whether the character belongs in a word: it is neither white space nor a token of its own.</summary>
    public static bool IsWordCharacter(char character) => !char.IsWhiteSpace(character) && !Punctuation.ContainsKey(character);
}
