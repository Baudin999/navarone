using Microsoft.CodeAnalysis.MSBuild;
using Navarone.Engine.Configuration;
using Navarone.Engine.Facts;
using Navarone.Engine.Providers;
using Navarone.Languages.CSharp.Facts;
using Navarone.Languages.CSharp.Loading;

namespace Navarone.Languages.CSharp;

/// <summary>
/// Reads C# through Roslyn: loads a solution, then turns its compilations into facts. What the given ignores match
/// yields no facts: no document of an ignored project and no ignored file is read.
/// </summary>
public sealed class CSharpProvider(Ignores ignores) : ILanguageProvider
{
    /// <inheritdoc />
    public string Language => "csharp";

    /// <summary>Whether the path names a .sln or .slnx file.</summary>
    public bool CanLoad(string path) =>
        File.Exists(path)
        && (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Loads the solution at the given path and resolves it into facts. They are complete unless MSBuild reported a
    /// failure or a project the ignores leave in misses a type, namespace or assembly it references.
    /// </summary>
    public async Task<CodebaseFacts> LoadAsync(string path, CancellationToken cancellationToken)
    {
        using var workspace = MSBuildWorkspace.Create();
        var (solution, failed) = await SolutionLoader.OpenAsync(workspace, path, cancellationToken).ConfigureAwait(false);
        var facts = await FactExtractor.ExtractAsync(solution, ignores, cancellationToken).ConfigureAwait(false);
        if (failed)
        {
            return facts with { Complete = false };
        }

        return facts;
    }
}
