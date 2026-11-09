using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Navarone.Engine.Events;

/// <summary>
/// Navarone's event log and its code store, in Navarone's own data folder. It is the only place that writes events to
/// the log and reads them back, and the only one that touches the code store. The log is events/YYYY-MM.jsonl, one
/// file per UTC month, one event per line, appended and never rewritten. The code store holds each piece of code once,
/// under the hash of its text, as code/&lt;hash&gt;, so an event names code by its hash and the log stays small. An event
/// that cannot be written is lost, never a failure: the handler says so on one line to the given error writer.
/// </summary>
[SuppressMessage("Naming", "CA1711", Justification = "The user named the one writer and reader of the log NavaroneEventHandler: it handles Navarone's events and is no .NET event delegate.")]
public sealed class NavaroneEventHandler(string folder, TimeProvider clock, TextWriter error)
{
    /// <summary>The data folder Navarone writes to: navarone under the user's data home, ~/.local/share unless XDG_DATA_HOME says otherwise.</summary>
    /// <remarks>The data home need not exist yet: Write and Store create the folders they write.</remarks>
    public static string DefaultFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), "navarone");

    /// <summary>The hash a piece of code is stored under: the SHA-256 of its UTF-8 text, in lower case hex.</summary>
    public static string HashOf(string code) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    /// <summary>
    /// Appends the given events to the log of the current UTC month, each stamped with the time now and the given
    /// place, in one write. Several writers may append at once, in one process or in several: each holds the log's lock
    /// while it writes, so no two appends interleave and none is lost. A log that cannot be written, such as a lock held
    /// longer than the patience, loses these events: the one line that WriteOrThrow's error carries goes to the error
    /// writer, and Write returns.
    /// </summary>
    public void Write(EventPlace place, IReadOnlyList<INavaroneEvent> events)
    {
        try
        {
            WriteOrThrow(place, events);
        }
        catch (InvalidDataException exception)
        {
            error.WriteLine($"navarone: {exception.Message}");
        }
    }

    /// <summary>
    /// Appends the events as Write does, for a command whose product is the event: a log that cannot be written throws
    /// an InvalidDataException that names the cause, and says nothing on the error writer.
    /// </summary>
    public void WriteOrThrow(EventPlace place, IReadOnlyList<INavaroneEvent> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        try
        {
            Append(place, events);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"the event log was not written, {events.Count} event(s) lost: {exception.Message}", exception);
        }
    }

    private void Append(EventPlace place, IReadOnlyList<INavaroneEvent> events)
    {
        var at = clock.GetUtcNow();
        var text = new StringBuilder();
        foreach (var happened in events)
        {
            text.Append(new LoggedEvent(at, place, happened).Line()).Append('\n');
        }

        new LogFolder(EventsDirectory(), clock).Append(at, Encoding.UTF8.GetBytes(text.ToString()));
    }

    /// <summary>
    /// Every event in the log, month by month and in the order written. A line still being written is not read yet and
    /// a torn line, one that is no JSON, is skipped. A line that is JSON and no event Navarone knows throws an
    /// InvalidDataException that names its file and line.
    /// </summary>
    public IReadOnlyList<LoggedEvent> Read()
    {
        return Read([]);
    }

    /// <summary>
    /// Every event in the log as Read does, and adds the file and line number of each torn line it skipped to the
    /// given list, each as "file line number", in the order written, from the same walk of the log.
    /// </summary>
    public IReadOnlyList<LoggedEvent> Read(ICollection<string> torn)
    {
        var events = new List<LoggedEvent>();
        foreach (var line in ReadLines())
        {
            if (line.Logged is { } logged)
            {
                events.Add(logged);
            }
            else
            {
                torn.Add(line.Where);
            }
        }

        return events;
    }

    /// <summary>
    /// Stores the code once under its hash, unless it is stored already, and returns the hash. Code that cannot be
    /// stored is lost: one line says so on the error writer, and the hash is returned all the same.
    /// </summary>
    public string Store(string code)
    {
        var hash = HashOf(code);
        try
        {
            Save(hash, code);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"navarone: the code under {hash} was not stored: {exception.Message}");
        }

        return hash;
    }

    private void Save(string hash, string code)
    {
        var directory = Path.Combine(folder, "code");
        var path = Path.Combine(directory, hash);
        if (File.Exists(path))
        {
            return;
        }

        // Two writers of one hash write the same text, so the later move changes nothing.
        Directory.CreateDirectory(directory);
        AtomicFile.Write(path, code);
    }

    /// <summary>The code stored under the given hash, or null when no code is stored under it.</summary>
    public string? Code(string hash)
    {
        if (hash.Length != 64 || !hash.All(char.IsAsciiHexDigitLower))
        {
            return null;
        }

        var path = Path.Combine(folder, "code", hash);
        if (!File.Exists(path))
        {
            return null;
        }

        return File.ReadAllText(path);
    }

    private string EventsDirectory() => Path.Combine(folder, "events");

    private List<LogLine> ReadLines()
    {
        var lines = new List<LogLine>();
        foreach (var month in new LogFolder(EventsDirectory(), clock).Months())
        {
            var complete = LogFolder.CompleteLines(month);
            for (var at = 0; at < complete.Count; at++)
            {
                lines.Add(LogLine.Parse(month, at + 1, complete[at]));
            }
        }

        return lines;
    }
}
