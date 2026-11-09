namespace Navarone.Engine.Clashes;

/// <summary>A subject's code as it stands in its file, Text, from Line of the file at Path.</summary>
internal sealed record SubjectCode(string Path, int Line, string Text);
