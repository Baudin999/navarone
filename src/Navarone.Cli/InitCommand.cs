using Navarone.Engine.Cache;
using Navarone.Engine.Configuration;

namespace Navarone.Cli;

/// <summary>`navarone init`: navarone.json at the repo root, so a repo opts in, and the facts cache in its .gitignore.</summary>
internal sealed class InitCommand(Session session)
{
    /// <summary>`navarone init`, which needs no codebase.</summary>
    public static Anywhere Init()
    {
        return new Anywhere("init", Costed: true, current => new InitCommand(current).InitAsync());
    }

    // Writes navarone.json at the repo root with the test projects ignored, and adds the facts cache folder to the
    // repo's .gitignore, and no other file. A navarone.json that is there already is left as it is, and so is a
    // .gitignore that ignores the folder already, and init says so of each.
    private async Task<int> InitAsync()
    {
        var path = NavaroneConfig.PathIn(session.RepoRoot);
        if (NavaroneConfig.Init(session.RepoRoot))
        {
            await session.Output.WriteLineAsync($"{path} written, with the test projects ignored").ConfigureAwait(false);
        }
        else
        {
            await session.Output.WriteLineAsync($"{path} exists already; init leaves it as it is").ConfigureAwait(false);
        }

        var ignore = CacheIgnore.PathIn(session.RepoRoot);
        if (CacheIgnore.Add(session.RepoRoot))
        {
            await session.Output.WriteLineAsync($"{ignore} ignores {CacheIgnore.Line}, the facts cache").ConfigureAwait(false);
        }
        else
        {
            await session.Output.WriteLineAsync($"{ignore} ignores {CacheIgnore.Line} already").ConfigureAwait(false);
        }

        return ExitCodes.Initialized;
    }
}
