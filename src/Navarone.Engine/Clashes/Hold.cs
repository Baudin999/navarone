namespace Navarone.Engine.Clashes;

/// <summary>
/// A problem a clash holds red, whatever its finding says: one the user has not judged yet, or one he answered replace
/// to while its subject's code is the code he answered about. Path and Line are where its subject is, Says what
/// `navarone` prints after them, and Code the hash of the subject's code now, or null when the subject is gone.
/// </summary>
public sealed record Hold(string Id, string Check, string Symbol, string Path, int Line, string Says, string? Code);
