using Navarone.Engine.Clashes;
using Navarone.Engine.Events;

namespace Navarone.Cli;

/// <summary>brain's commands: what brain's window and brain read of Navarone's event log. Each writes nothing, so it runs from any directory.</summary>
internal sealed class BrainCommands(Session session)
{
    /// <summary>`navarone disputes --json`: the clashes brain's window lists.</summary>
    public static Anywhere Disputes()
    {
        return new Anywhere("disputes", Costed: false, current => new BrainCommands(current).DisputesAsync());
    }

    /// <summary>`navarone events --json`: the event log for brain.</summary>
    public static Anywhere Events()
    {
        return new Anywhere("events", Costed: false, current => new BrainCommands(current).EventsAsync());
    }

    /// <summary>`navarone code &lt;hash&gt;`: the code stored under the hash.</summary>
    public static Anywhere Code(string hash)
    {
        return new Anywhere("code", Costed: false, current => new BrainCommands(current).CodeAsync(hash));
    }

    // `navarone disputes --json`: the questions brain's window lists, one per clash the user has not judged yet, as one
    // JSON document. It reads the event log and the code store.
    private async Task<int> DisputesAsync()
    {
        await session.Output.WriteLineAsync(Questions.Document(session.Events)).ConfigureAwait(false);
        return ExitCodes.Listed;
    }

    // `navarone events --json`: every event in the log as one JSON document, for brain to read. A torn line is skipped
    // and named, by file and line, in one line on stderr.
    private async Task<int> EventsAsync()
    {
        var torn = new List<string>();
        var read = session.Events.Read(torn);
        if (torn.Count > 0)
        {
            var word = "lines";
            if (torn.Count == 1)
            {
                word = "line";
            }

            var places = string.Join(", ", torn);
            await session.Error.WriteLineAsync($"navarone: skipped torn {word}: {places}").ConfigureAwait(false);
        }

        await session.Output.WriteLineAsync(LoggedEvent.Document(read)).ConfigureAwait(false);
        return ExitCodes.Printed;
    }

    // `navarone code <hash>`: the code stored under the hash an event names, as it was written. A hash no code is stored
    // under is a usage error.
    private async Task<int> CodeAsync(string hash)
    {
        var code = session.Events.Code(hash);
        if (code is null)
        {
            await session.Error.WriteLineAsync($"navarone: no code stored under {hash}").ConfigureAwait(false);
            return ExitCodes.UsageError;
        }

        await session.Output.WriteAsync(code).ConfigureAwait(false);
        return ExitCodes.Printed;
    }
}
