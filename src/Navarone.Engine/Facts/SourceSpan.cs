namespace Navarone.Engine.Facts;

/// <summary>
/// A stretch of a file and its text as written. Lines and columns are positions in the file and start at 1;
/// EndCol is the column of the span's last character.
/// </summary>
public sealed record SourceSpan(int StartLine, int StartCol, int EndLine, int EndCol, string Text);
