using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Navarone.Engine.Events;

/// <summary>
/// An event as the log holds it: when it happened, in UTC, where, and the event itself. One line of the log is one
/// flat JSON object: the event's type, its time, its place and the event's own fields.
/// </summary>
public sealed record LoggedEvent(DateTimeOffset At, EventPlace Place, INavaroneEvent Event)
{
    /// <summary>
    /// How an event's fields are written: camel case, positions and states as lower case words, a null left out, and
    /// code and reasons as written instead of \u escapes.
    /// </summary>
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// The event read back from one line of the log. A line that is not such an event throws a JsonException, and so
    /// does an event whose type Navarone does not know.
    /// </summary>
    public static LoggedEvent FromJson(JsonObject line)
    {
        var at = line["at"]?.GetValue<DateTimeOffset>() ?? throw new JsonException("it has no at");
        var repo = line["repo"]?.GetValue<string>() ?? throw new JsonException("it has no repo");
        var worktree = line["worktree"]?.GetValue<string>() ?? throw new JsonException("it has no worktree");
        var branch = line["branch"]?.GetValue<string>();
        var head = line["head"]?.GetValue<string>();
        var happened = line.Deserialize<INavaroneEvent>(Options) ?? throw new JsonException("it holds no event");
        return new LoggedEvent(at, new EventPlace(repo, worktree, branch, head), happened);
    }

    /// <summary>The event as one flat JSON object: type, at, repo, worktree, branch and head, then the event's own fields.</summary>
    public JsonObject Json()
    {
        var fields = JsonSerializer.SerializeToNode(Event, Options)!.AsObject();
        var line = new JsonObject
        {
            ["type"] = fields["type"]!.DeepClone(),
            ["at"] = At.ToUniversalTime(),
            ["repo"] = Place.Repo,
            ["worktree"] = Place.Worktree,
            ["branch"] = Place.Branch,
            ["head"] = Place.Head,
        };
        foreach (var (name, value) in fields)
        {
            if (name == "type")
            {
                continue;
            }

            line[name] = value?.DeepClone();
        }

        return line;
    }

    /// <summary>
    /// The given events as one JSON document, `{"source":"navarone","events":[...]}`, each event as its line of the
    /// log holds it. `navarone events --json` prints it.
    /// </summary>
    public static string Document(IReadOnlyList<LoggedEvent> events)
    {
        var lines = new JsonArray();
        foreach (var logged in events)
        {
            lines.Add(logged.Json());
        }

        return new JsonObject { ["source"] = "navarone", ["events"] = lines }.ToJsonString(Options);
    }

    /// <summary>The event as one line of the log, without its line break.</summary>
    public string Line() => Json().ToJsonString(Options);
}
