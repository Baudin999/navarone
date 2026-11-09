using Navarone.Engine;
using Navarone.Engine.Events;

namespace Navarone.Cli;

/// <summary>
/// A form of `navarone`, parsed from its arguments: its name, which is the name its cost is logged under, and whether it
/// logs a cost. Whether it needs the repo is its kind: an <see cref="Anywhere"/> or an <see cref="InRepo"/>.
/// </summary>
internal abstract record Command(string Form, bool Costed)
{
    /// <summary>Runs the form in the session and returns what the command exits with.</summary>
    public abstract Task<int> RunAsync(Session session);
}

/// <summary>A form that needs no codebase, so it runs from any directory.</summary>
internal sealed record Anywhere(string Form, bool Costed, Func<Session, Task<int>> Run) : Command(Form, Costed)
{
    public override Task<int> RunAsync(Session session)
    {
        return Run(session);
    }
}

/// <summary>A form that needs the repo: the engine wired for its root, and every codebase under it. A repo with none is a usage error.</summary>
internal sealed record InRepo(string Form, bool Costed, Func<Repo, Task<int>> Run) : Command(Form, Costed)
{
    public override async Task<int> RunAsync(Session session)
    {
        var setup = session.Wiring(session.RepoRoot, session.Events);
        var codebases = FileLocations.Codebases(setup.RepoRoot, setup.Provider);
        if (codebases.Count == 0)
        {
            await session.Error.WriteLineAsync($"navarone: no .sln or .slnx under the repo root {setup.RepoRoot}").ConfigureAwait(false);
            return ExitCodes.UsageError;
        }

        var repo = new Repo(setup, codebases, session.Output, session.Error);
        return await Run(repo).ConfigureAwait(false);
    }
}

/// <summary>What a form that needs the repo runs with: the engine wired for the repo, its codebases, and where it prints.</summary>
internal sealed record Repo(EngineSetup Setup, IReadOnlyList<string> Codebases, TextWriter Output, TextWriter Error)
{
    /// <summary>The code of the repo: its codebases, read by the setup's provider.</summary>
    public Code Code => new(Setup.Provider, Setup.RepoRoot, Codebases);
}

/// <summary>What a form runs with: the event log, the repo root, the wiring of the engine for that root, and where it prints.</summary>
internal sealed record Session(
    NavaroneEventHandler Events,
    string RepoRoot,
    Func<string, NavaroneEventHandler, EngineSetup> Wiring,
    TextWriter Output,
    TextWriter Error);
