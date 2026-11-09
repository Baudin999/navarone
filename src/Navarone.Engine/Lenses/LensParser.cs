namespace Navarone.Engine.Lenses;

/// <summary>
/// Parses a lens by its grammar:
/// <code>
/// lens  = kind name (":" word)? view*
/// view  = step ("[" view* "]")?
/// step  = word (":" arg ("," arg)*)?
/// arg   = word (":" word)?
/// </code>
/// An arg with a colon names its parameter, such as lines:5.
/// A lens that does not follow it is an error that says what was expected and where.
/// </summary>
internal sealed class LensParser
{
    private readonly string text;
    private readonly List<LensToken> tokens;
    private int next;

    private LensParser(string text)
    {
        this.text = text;
        tokens = LensLexer.Tokens(text);
    }

    /// <summary>The lens the given text writes.</summary>
    public static Lens Parse(string text) => new LensParser(text).Lens();

    private Lens Lens()
    {
        var kind = Expect(LensTokenKind.Word, "a kind, such as method").Word;
        var name = Expect(LensTokenKind.Word, "a name, such as Type.Method").Word;
        LensWord? overload = Accept(LensTokenKind.Colon) is null ? null : Expect(LensTokenKind.Word, "an overload number after :").Word;
        var views = Views();
        Expect(LensTokenKind.End, "a view");
        return new Lens(text, kind, name, overload, views);
    }

    private List<LensView> Views()
    {
        var views = new List<LensView>();
        while (At(LensTokenKind.Word))
        {
            views.Add(View());
        }

        return views;
    }

    private LensView View()
    {
        var word = Expect(LensTokenKind.Word, "a view").Word;
        var values = new List<LensValue>();
        if (Accept(LensTokenKind.Colon) is not null)
        {
            values.Add(Value("a value after :"));
            while (Accept(LensTokenKind.Comma) is not null)
            {
                values.Add(Value("a value after ,"));
            }
        }

        if (Accept(LensTokenKind.Open) is not { } open)
        {
            return new LensView(word, values, []);
        }

        var views = Views();
        Expect(LensTokenKind.Close, $"a view, or ] to close the [ at column {open.Column}");
        return new LensView(word, values, views);
    }

    private LensValue Value(string expected)
    {
        var first = Expect(LensTokenKind.Word, expected).Word;
        if (Accept(LensTokenKind.Colon) is null)
        {
            return new LensValue(null, first);
        }

        var value = Expect(LensTokenKind.Word, $"a value for {first.Text} after :").Word;
        return new LensValue(first, value);
    }

    // The lexer ends every lens with its end, which no word, colon or bracket matches, so the next token is always there.
    private bool At(LensTokenKind kind) => tokens[next].Kind == kind;

    private LensToken? Accept(LensTokenKind kind)
    {
        if (!At(kind))
        {
            return null;
        }

        next++;
        return tokens[next - 1];
    }

    private LensToken Expect(LensTokenKind kind, string expected) => Accept(kind) ?? throw Unexpected(expected);

    private InvalidDataException Unexpected(string expected)
    {
        var found = tokens[next];
        return LensError.At(text, found.Column, $"expected {expected}, not {found.Text}");
    }
}
