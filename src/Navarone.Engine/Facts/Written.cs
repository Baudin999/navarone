namespace Navarone.Engine.Facts;

/// <summary>
/// A subject's code as it stands in its file: Text is the whole lines it spans, comments included, and Line the line
/// of the file the text starts on. A control flow fact's line falls inside them. Code, which voids a reason when it
/// changes, leaves the navarone comments out; this is what the user reads.
/// </summary>
public sealed record Written(int Line, string Text)
{
    /// <summary>The last line of the file the text spans.</summary>
    public int EndLine => Line + Text.Count(character => character == '\n');
}
