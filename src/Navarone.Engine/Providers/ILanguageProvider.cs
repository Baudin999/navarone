using Navarone.Engine.Facts;

namespace Navarone.Engine.Providers;

/// <summary>
/// Knows one language. Parses a codebase written in it and hands the engine resolved facts.
/// Adding a language means implementing this interface; the engine and the metrics do not change.
/// </summary>
public interface ILanguageProvider
{
    /// <summary>The language this provider reads, such as "csharp".</summary>
    string Language { get; }

    /// <summary>Whether this provider can load the codebase at the given path. The CLI analyses every file under the repo root it can load.</summary>
    bool CanLoad(string path);

    /// <summary>Loads the codebase at the given path and resolves it into facts.</summary>
    Task<CodebaseFacts> LoadAsync(string path, CancellationToken cancellationToken);
}
