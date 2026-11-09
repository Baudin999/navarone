using Navarone.Engine.Facts;

namespace Navarone.Engine.Findings;

/// <summary>
/// A subject's code where it stands: its file, relative to its codebase, the lines it spans and its source text as it
/// stands in the file, comments included, so a line of the file is line StartLine plus its index in Code.
/// </summary>
public sealed record CodePiece(string Path, int StartLine, int EndLine, string Code)
{
    /// <summary>The code of a method.</summary>
    public static CodePiece Of(MethodFacts method) => new(method.Location.Path, method.Written.Line, method.Written.EndLine, method.Written.Text);
}
