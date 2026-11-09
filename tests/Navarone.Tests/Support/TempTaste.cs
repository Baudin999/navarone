namespace Navarone.Tests.Support;

/// <summary>A NAVARONE.md with three rules of taste, deleted after the test, so no test reads the user's own.</summary>
internal sealed class TempTaste : IDisposable
{
    /// <summary>The second rule, as a refusal that names it carries it.</summary>
    public const string Second = "One line does one thing: no crammed condition, no step hidden in an argument.";

    private readonly string folder = Directory.CreateTempSubdirectory("navarone-taste-").FullName;

    public TempTaste()
    {
        Path = System.IO.Path.Combine(folder, "NAVARONE.md");
        File.WriteAllText(Path, """
            # NAVARONE.md

            ## The rules

            Code is for the next change, not for looks.

            1. Imperative: statements that do one step after another.
            2. One line does one thing: no crammed condition,
               no step hidden in an argument.
            3. Plain over clever.

            ## The process

            4. Not a rule.
            """);
    }

    /// <summary>Where the NAVARONE.md is.</summary>
    public string Path { get; }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
