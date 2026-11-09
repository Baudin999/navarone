namespace Navarone.Engine.Events;

/// <summary>
/// The code a reason defends: Code is the hash of the subject's code in the code store, Path the file it is in,
/// relative to its codebase's directory as the lens names it, StartLine the line its code starts on and Line the line
/// of the finding, or StartLine when the problem has no finding now. Context is the lens that makes the subject a
/// method's subject, or null for a subject a lens cannot show, such as a class.
/// </summary>
public sealed record DefendedCode(string Code, string Path, int StartLine, int Line, string? Context);
