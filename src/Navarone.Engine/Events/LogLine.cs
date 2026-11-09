using System.Text.Json;
using System.Text.Json.Nodes;

namespace Navarone.Engine.Events;

/// <summary>One complete line of a month file: an event, or a torn line that is no JSON.</summary>
internal sealed class LogLine
{
    private LogLine(string where, LoggedEvent? logged)
    {
        Where = where;
        Logged = logged;
    }

    /// <summary>The file and line number of the line, as a message names them.</summary>
    public string Where { get; }

    /// <summary>The event the line holds, or null when the line is torn: a writer died in the middle of it.</summary>
    public LoggedEvent? Logged { get; }

    /// <summary>
    /// Reads one line. A line that is no JSON is torn. A line that is JSON and no event Navarone knows throws an
    /// InvalidDataException that names its file and line.
    /// </summary>
    public static LogLine Parse(string month, int number, string line)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(line);
        }
        catch (JsonException)
        {
            return new LogLine($"{month} line {number}", null);
        }

        try
        {
            var logged = LoggedEvent.FromJson(node as JsonObject ?? throw new JsonException("it is no JSON object"));
            return new LogLine($"{month} line {number}", logged);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or NotSupportedException or FormatException)
        {
            throw new InvalidDataException($"{month} line {number} is no Navarone event: {exception.Message}", exception);
        }
    }
}
