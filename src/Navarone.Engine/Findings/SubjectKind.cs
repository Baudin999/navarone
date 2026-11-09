namespace Navarone.Engine.Findings;

/// <summary>What a finding is about. SPEC.md's table names the subject of each check.</summary>
public enum SubjectKind
{
    /// <summary>One method, constructor or accessor.</summary>
    Method,

    /// <summary>One class or struct.</summary>
    Class,

    /// <summary>One module, such as a project or an assembly.</summary>
    Module,
}
