using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Navarone.Engine.Facts;

namespace Navarone.Engine.Cache;

/// <summary>
/// Facts as the JSON of a cache entry, and back. Reading is strict: a missing value, a null where the facts take none,
/// or a member the facts do not have, throws a JsonException, so an entry of another shape is never read as facts.
/// </summary>
internal static class FactsJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OnlyWhatReadingSets } },
        Converters = { new ControlFlowConverter() },
    };

    /// <summary>The facts as JSON. Facts whose control flow is part of a fact that does not come before it in its method throw a JsonException.</summary>
    public static string Write(CodebaseFacts facts) => JsonSerializer.Serialize(facts, Options);

    /// <summary>The facts the JSON holds. JSON that holds no facts, or not these facts' shape, throws a JsonException.</summary>
    public static CodebaseFacts Read(Stream json)
    {
        var facts = JsonSerializer.Deserialize<CodebaseFacts>(json, Options);
        if (facts is null)
        {
            throw new JsonException("the entry holds no facts");
        }

        return facts;
    }

    // Leaves out every property reading cannot set, such as a method's Calls, which its CallSites build, so the JSON
    // holds each fact once.
    private static void OnlyWhatReadingSets(JsonTypeInfo type)
    {
        if (type.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        for (var at = type.Properties.Count - 1; at >= 0; at--)
        {
            var property = type.Properties[at];
            if (property.Set is null && property.AssociatedParameter is null)
            {
                type.Properties.RemoveAt(at);
            }
        }
    }

    // A method's control flow, with the fact each is part of as the place of that fact in the list. A metric tells the
    // parts of a fact by reference, so reading makes each fact part of the very fact read before it.
    // navarone: 0000087
    private sealed class ControlFlowConverter : JsonConverter<IReadOnlyList<ControlFlowFact>>
    {
        public override IReadOnlyList<ControlFlowFact> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var entries = JsonSerializer.Deserialize<List<ControlFlowEntry>>(ref reader, options);
            if (entries is null)
            {
                throw new JsonException("a method's control flow is null");
            }

            var facts = new List<ControlFlowFact>(entries.Count);
            foreach (var entry in entries)
            {
                var whole = Whole(facts, entry.PartOf);
                facts.Add(new ControlFlowFact(entry.Kind, entry.NestingDepth, entry.Span, entry.Continues, entry.Construct, whole));
            }

            return facts;
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyList<ControlFlowFact> value, JsonSerializerOptions options)
        {
            var places = new Dictionary<ControlFlowFact, int>(ReferenceEqualityComparer.Instance);
            var entries = new List<ControlFlowEntry>(value.Count);
            foreach (var fact in value)
            {
                var partOf = PlaceOf(places, fact.PartOf);
                entries.Add(new ControlFlowEntry(fact.Kind, fact.NestingDepth, fact.Span, fact.Continues, fact.Construct, partOf));
                places.TryAdd(fact, entries.Count - 1);
            }

            JsonSerializer.Serialize(writer, entries, options);
        }

        // The fact read at the given place, which must come before the one read now.
        private static ControlFlowFact? Whole(List<ControlFlowFact> facts, int? place)
        {
            if (place is null)
            {
                return null;
            }

            if (place < 0 || place >= facts.Count)
            {
                throw new JsonException($"a control flow fact is part of fact {place}, which does not come before it");
            }

            return facts[place.Value];
        }

        // The place of the given fact among those written before, or null for no fact.
        private static int? PlaceOf(Dictionary<ControlFlowFact, int> places, ControlFlowFact? whole)
        {
            if (whole is null)
            {
                return null;
            }

            if (!places.TryGetValue(whole, out var place))
            {
                throw new JsonException("a control flow fact is part of a fact that does not come before it in its method");
            }

            return place;
        }
    }

    // One control flow fact as an entry writes it.
    private sealed record ControlFlowEntry(ControlFlowKind Kind, int NestingDepth, SourceSpan Span, bool Continues, string Construct, int? PartOf);
}
