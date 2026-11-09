namespace Navarone.Engine.Clashes;

/// <summary>
/// A problem a clash holds red, whatever its finding says: one the user has not judged yet, or one he answered replace
/// to while its subject's code is the code he answered about. Path, Line and EndLine are where its subject is, or where the builder defended it when the subject is gone, Says what
/// `navarone` prints after them, and Text the subject's code now as it is written, or null when the subject is gone.
/// </summary>
public sealed record Hold(string Id, string Check, string Symbol, string Path, int Line, int EndLine, string Says, string? Text);
