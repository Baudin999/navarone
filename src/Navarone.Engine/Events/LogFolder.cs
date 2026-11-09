using System.Globalization;
using System.Text;

namespace Navarone.Engine.Events;

/// <summary>
/// The events folder at the file level: one month file per UTC month and the lock every append holds. It appends
/// bytes and reads back lines of text, and knows no event. Only appends take the lock and the clock, for its patience.
/// </summary>
internal sealed class LogFolder(string directory, TimeProvider clock)
{
    // How long an append waits for another process to finish its append before it gives up.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private string LockPath => Path.Combine(directory, ".lock");

    /// <summary>
    /// Appends the bytes to the month file of the given time while holding the lock, so no two appends interleave. When
    /// a writer died mid-line, the bytes start a line of their own instead of gluing onto the torn one.
    /// </summary>
    public void Append(DateTimeOffset at, byte[] bytes)
    {
        Directory.CreateDirectory(directory);
        var month = Path.Combine(directory, at.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".jsonl");
        using var held = Hold();
        using var log = new FileStream(month, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        if (EndsInTornLine(month))
        {
            // Under the lock no writer is still writing, so a last byte that is no line break was left by a dead one.
            log.WriteByte((byte)'\n');
        }

        log.Write(bytes);
        log.Flush(flushToDisk: true);
    }

    /// <summary>The month files, oldest first. A folder that is not there holds none.</summary>
    public IReadOnlyList<string> Months()
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return [.. Directory.GetFiles(directory, "*.jsonl").Order(StringComparer.Ordinal)];
    }

    /// <summary>The complete lines of a month file. The text after the last line break is a line another process is still writing.</summary>
    public static List<string> CompleteLines(string month)
    {
        string text;
        using (var log = new FileStream(month, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(log, Encoding.UTF8))
        {
            text = reader.ReadToEnd();
        }

        var lines = text.Split('\n');
        return [.. lines.Take(lines.Length - 1)];
    }

    // Whether the file ends in the middle of a line: it is not empty and its last byte is no line break.
    private static bool EndsInTornLine(string month)
    {
        using var log = new FileStream(month, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (log.Length == 0)
        {
            return false;
        }

        log.Seek(-1, SeekOrigin.End);
        return log.ReadByte() != '\n';
    }

    // Takes the lock on the lock file, waiting while another writer holds it, and gives it back when disposed. A file
    // opened with FileShare.None holds an exclusive lock on it, which a second open refuses with an IOException, from
    // another thread of this process as from another process.
    private FileStream Hold()
    {
        var deadline = clock.GetUtcNow() + Patience;
        while (true)
        {
            try
            {
                return new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (clock.GetUtcNow() < deadline)
            {
                Thread.Sleep(10);
            }
        }
    }
}
