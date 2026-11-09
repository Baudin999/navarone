using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Navarone.Engine.Facts;
using Navarone.Engine.Findings;

namespace Navarone.Engine.Reasons;

/// <summary>
/// The model of reasons.json at the root of the analysed repo: every problem by its id, with its resolution. Only
/// Navarone writes it; an agent never edits it and answers through `navarone &lt;id&gt; "&lt;reason&gt;"` instead.
/// Granted is the last id Navarone granted, so an id is never granted twice, not even after its problem is removed.
/// </summary>
public sealed record ReasonsFile(int Granted, IReadOnlyList<RecordedProblem> Problems)
{
    /// <summary>The file name the engine reads and writes.</summary>
    public const string FileName = "reasons.json";

    /// <summary>
    /// How Navarone writes its JSON: camel case, each id, symbol and fingerprint as its plain string.
    /// </summary>
    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new ValueConverter<ProblemId>(value => new ProblemId(value), id => id.Value),
            new ValueConverter<SymbolId>(value => new SymbolId(value), symbol => symbol.Value),
            new ValueConverter<CodeFingerprint>(value => new CodeFingerprint(value), fingerprint => fingerprint.Value),
        },
    };

    /// <summary>
    /// Reads reasons.json from the root of the analysed repo. A missing file holds no problems. A file that is not
    /// valid JSON for this model throws an InvalidDataException.
    /// </summary>
    public static ReasonsFile Load(string repoRoot)
    {
        var path = Path.Combine(repoRoot, FileName);
        if (!File.Exists(path))
        {
            return new ReasonsFile(0, []);
        }

        try
        {
            return JsonSerializer.Deserialize<ReasonsFile>(File.ReadAllText(path), Options)
                ?? throw new InvalidDataException($"{FileName} holds no problems object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{FileName} is not valid: {exception.Message}", exception);
        }
    }

    /// <summary>Writes reasons.json into the root of the analysed repo, problems in id order. Only the engine calls this. A file that cannot be written throws an InvalidDataException.</summary>
    public void Save(string repoRoot)
    {
        var ordered = this with { Problems = [.. Problems.OrderBy(problem => problem.Id.Value, StringComparer.Ordinal)] };
        AtomicFile.WriteOrRefuse(Path.Combine(repoRoot, FileName), JsonSerializer.Serialize(ordered, Options) + "\n");
    }

    /// <summary>The problem with the given id, or null.</summary>
    public RecordedProblem? For(ProblemId id) => Problems.FirstOrDefault(problem => problem.Id == id);

    /// <summary>The problem whose check and symbol match the finding, or null when the finding is new.</summary>
    public RecordedProblem? For(Finding finding) =>
        Problems.FirstOrDefault(problem => problem.Check == finding.Check && problem.Symbol == finding.Subject);

    /// <summary>
    /// The problem a renamed subject carries over through its `// navarone: &lt;id&gt;` comment, or null. It is the
    /// problem of one of the given ids written at the finding's code, of the finding's check, whose recorded symbol
    /// no longer exists in the facts. When the recorded symbol still exists, the id at this code is a copy and
    /// does not count.
    /// </summary>
    public RecordedProblem? Renamed(Finding finding, IReadOnlyList<string> idsAtCode, CodebaseFacts facts)
    {
        var symbols = facts.Symbols();
        return Problems.FirstOrDefault(problem =>
            idsAtCode.Contains(problem.Id.Value)
            && problem.Check == finding.Check
            && !symbols.Contains(problem.Symbol));
    }

    /// <summary>Whether Navarone granted the given id: a number from 1 up to the last id granted.</summary>
    public bool HasGranted(ProblemId id)
    {
        if (!int.TryParse(id.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        return number >= 1 && number <= Granted;
    }

    /// <summary>The id Navarone grants to the next new problem.</summary>
    public ProblemId NextId() => ProblemId.Numbered(Granted + 1);

    /// <summary>
    /// This file with the given problem in place of any earlier entry under the same id. A problem under a new id
    /// counts that id as granted.
    /// </summary>
    public ReasonsFile With(RecordedProblem problem) => new(
        Math.Max(Granted, problem.Id.Number),
        [.. Problems.Where(recorded => recorded.Id != problem.Id), problem]);

    /// <summary>
    /// This file without the problems whose symbol no longer exists in the facts. The engine carries renamed
    /// problems over to their new symbol first, so only problems whose code is gone are removed.
    /// </summary>
    public ReasonsFile WithoutGoneCode(CodebaseFacts facts)
    {
        var symbols = facts.Symbols();
        return this with { Problems = [.. Problems.Where(problem => symbols.Contains(problem.Symbol))] };
    }

    /// <summary>Writes a single value record struct as its plain string.</summary>
    private sealed class ValueConverter<T>(Func<string, T> read, Func<T, string> write) : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            read(reader.GetString() ?? throw new JsonException($"{FileName} holds a null where a {typeof(T).Name} belongs."));

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteStringValue(write(value));
    }
}
