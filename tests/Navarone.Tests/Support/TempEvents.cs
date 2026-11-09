using Navarone.Engine;
using Navarone.Engine.Clashes;
using Navarone.Engine.Events;

namespace Navarone.Tests.Support;

/// <summary>A data folder for Navarone's event log and code store, deleted after the test, so no test writes to the user's own. It also stands in for brain's answers and the user's NAVARONE.md.</summary>
internal sealed class TempEvents : IDisposable
{
    public string Folder { get; } = Directory.CreateTempSubdirectory("navarone-events-").FullName;

    /// <summary>The event log in this folder, on the system clock.</summary>
    public NavaroneEventHandler Handler => new(Folder, TimeProvider.System, TextWriter.Null);

    /// <summary>The event log in this folder, with every event stamped at the given time.</summary>
    public NavaroneEventHandler At(DateTimeOffset now) => new(Folder, new FixedClock(now), TextWriter.Null);

    /// <summary>The event log in this folder, telling the given writer of every event it could not write.</summary>
    public NavaroneEventHandler Reporting(TextWriter error) => new(Folder, TimeProvider.System, error);

    /// <summary>The given setup with brain's answers and the global NAVARONE.md in this folder, where none exists, so no test reads the user's ~/.claude or brain's cache.</summary>
    public EngineSetup Isolate(EngineSetup setup) => setup with
    {
        Answers = new BrainAnswers(Path.Combine(Folder, "answers")),
        Taste = Path.Combine(Folder, "NAVARONE.md"),
    };

    public void Dispose() => Directory.Delete(Folder, recursive: true);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
