using Navarone.Engine.Facts;
using Navarone.Engine.Providers;

namespace Navarone.Tests.Support;

/// <summary>A provider that hands the engine hand built facts per path, so engine tests never go through Roslyn.</summary>
internal sealed class FakeProvider : ILanguageProvider
{
    /// <summary>The facts each path loads as. Tests change them between runs.</summary>
    public Dictionary<string, CodebaseFacts> Codebases { get; } = new(StringComparer.Ordinal);

    public string Language => "fake";

    /// <summary>A path loads when it has facts.</summary>
    public bool CanLoad(string path) => Codebases.ContainsKey(path);

    public Task<CodebaseFacts> LoadAsync(string path, CancellationToken cancellationToken) => Task.FromResult(Codebases[path]);
}
