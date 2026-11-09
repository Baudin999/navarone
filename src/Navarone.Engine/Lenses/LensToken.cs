namespace Navarone.Engine.Lenses;

/// <summary>What a stretch of a lens's text is to its lexer.</summary>
internal enum LensTokenKind
{
    /// <summary>A run of anything else: a kind, a name, a view or a value.</summary>
    Word,

    /// <summary>`:`, which puts a view's values after it.</summary>
    Colon,

    /// <summary>`,`, which separates a view's values.</summary>
    Comma,

    /// <summary>`[`, which opens a view's own views.</summary>
    Open,

    /// <summary>`]`, which closes them.</summary>
    Close,

    /// <summary>The end of the lens, after its last character.</summary>
    End,
}

/// <summary>One token of a lens: its kind, its text and the column of the lens it starts at, from 1.</summary>
internal sealed record LensToken(LensTokenKind Kind, string Text, int Column)
{
    /// <summary>The token as a word of the lens.</summary>
    public LensWord Word => new(Text, Column);
}
