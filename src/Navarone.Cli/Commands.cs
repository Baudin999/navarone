using Navarone.Engine;
using Navarone.Engine.Events;

namespace Navarone.Cli;

/// <summary>Runs `navarone`: reads its arguments as one of its forms, runs that form and logs its cost.</summary>
internal static class Commands
{
    /// <summary>
    /// Runs `navarone` with the given arguments from the given directory, with the engine wired for the repo root and the
    /// given event log by the given wiring. A form an agent reads the output of then logs its cost: its name and the
    /// characters it printed divided by four. A form that ended in a usage error logs no cost. A repo with no codebase
    /// under its root, a navarone.json or reasons.json it cannot read, a navarone.json that raises a limit above its
    /// default, a repo where `navarone` finds no commit to measure the change from, an id that matches no problem, a
    /// keep or replace with nothing to accept or refuse, a replace that names no rule of NAVARONE.md, a large change of
    /// a problem whose code does not carry its id, a settled of a problem with no large change or in a clash, an event
    /// log Navarone cannot read, a keep, replace, large, settled or reason whose event it cannot write, or a lens
    /// Navarone cannot show, is a usage error: one line on stderr, exit 2. A lens's error adds the lens with a caret
    /// under where it is wrong.
    /// </summary>
    public static async Task<int> RunAsync(
        string[] args,
        string currentDirectory,
        NavaroneEventHandler events,
        Func<string, NavaroneEventHandler, EngineSetup> wiring,
        TextWriter output,
        TextWriter error)
    {
        try
        {
            var command = Grammar.Parse(args);
            var printed = new CountingWriter(output);
            var session = new Session(events, FileLocations.RepoRoot(currentDirectory), wiring, printed, error);
            var exit = await command.RunAsync(session).ConfigureAwait(false);
            if (command.Costed && exit != ExitCodes.UsageError)
            {
                var tokens = Tokens.Of(printed.Characters);
                events.Write(EventPlace.Of(session.RepoRoot), [new CostEvent(command.Form, tokens)]);
            }

            return exit;
        }
        catch (InvalidDataException exception)
        {
            await error.WriteLineAsync($"navarone: {exception.Message}").ConfigureAwait(false);
            return ExitCodes.UsageError;
        }
    }
}
