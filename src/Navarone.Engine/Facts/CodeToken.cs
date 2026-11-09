namespace Navarone.Engine.Facts;

/// <summary>
/// One token of a method's body. Shape is the token as its provider normalised it, so a renamed copy has the shape of
/// its original: `id` for any identifier, `lit` for any literal, and the token as written for a keyword or punctuation.
/// Text is the token as written. Comments and whitespace are no tokens. Line is the line of the file the token starts
/// on.
/// </summary>
public readonly record struct CodeToken(string Shape, string Text, int Line)
{
    /// <summary>The shape of every identifier.</summary>
    public const string Identifier = "id";

    /// <summary>The shape of every literal.</summary>
    public const string Literal = "lit";
}
