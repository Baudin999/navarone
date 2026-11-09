using Navarone.Engine.Cache;
using Navarone.Engine.Facts;
using Navarone.Engine.Providers;

namespace Navarone.Engine;

/// <summary>
/// The code a command reads: the codebases at the given paths, such as each solution, in the repo at RepoRoot, read by
/// the provider. Every command that reads the code builds its facts here, so the facts cache serves them all.
/// </summary>
public sealed record Code(ILanguageProvider Provider, string RepoRoot, IReadOnlyList<string> Codebases)
{
    /// <summary>
    /// The merged facts of each codebase, loaded on its own, so a type two codebases share is one type. The facts of a
    /// version of the code are kept in the repo's facts cache, so the next command at that version reads them instead
    /// of loading every codebase again. Outside a repository with a commit there is no cache, and facts the provider
    /// could not load completely are never kept: both are loaded every time.
    /// </summary>
    public async Task<CodebaseFacts> FactsAsync(CancellationToken cancellationToken)
    {
        var cache = FactsCache.For(this);
        if (cache is null)
        {
            return await MergedAsync(cancellationToken).ConfigureAwait(false);
        }

        var cached = cache.Read();
        if (cached is not null)
        {
            return cached;
        }

        var facts = await MergedAsync(cancellationToken).ConfigureAwait(false);
        if (facts.Complete)
        {
            cache.Write(facts);
        }

        return facts;
    }

    // Loads each codebase on its own and merges their facts, so a type two codebases share is one type. The merged
    // facts are complete only when every codebase loaded completely.
    private async Task<CodebaseFacts> MergedAsync(CancellationToken cancellationToken)
    {
        var types = new List<TypeFacts>();
        var complete = true;
        foreach (var path in Codebases)
        {
            var facts = await Provider.LoadAsync(path, cancellationToken).ConfigureAwait(false);
            types.AddRange(facts.Types);
            if (!facts.Complete)
            {
                complete = false;
            }
        }

        return new CodebaseFacts([.. types.DistinctBy(type => type.Symbol)], complete);
    }
}
