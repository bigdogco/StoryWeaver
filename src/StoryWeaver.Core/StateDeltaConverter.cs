using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StoryWeaver.Core;

/// <summary>
/// Reads and writes <see cref="StateDelta"/> without caring where the <c>kind</c> property
/// appears in the object.
///
/// <b>Why this exists.</b> System.Text.Json's built-in polymorphism requires the type
/// discriminator to be the <i>first</i> property in the JSON object. A model has no reason
/// to honour that: one OpenRouter provider emitted <c>kind</c> first and deserialization
/// worked, while another emitted properties alphabetically — putting <c>kind</c> after
/// <c>evidence</c> — and every delta failed to parse. Both outputs were schema-conformant
/// and both were valid JSON. The dependency on ordering was ours.
///
/// This is the routing hazard in an unexpected costume: the same model id, the same schema,
/// different upstream provider, different property order. Anything that depends on the
/// *shape* of a response beyond what the schema guarantees is a latent version of this bug.
/// </summary>
public sealed class StateDeltaConverter : JsonConverter<StateDelta>
{
    private const string Discriminator = "kind";

    private static readonly Dictionary<string, Type> KindToType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["character_moved"] = typeof(CharacterMoved),
        ["player_moved"] = typeof(PlayerMoved),
        ["status_changed"] = typeof(StatusChanged),
        ["mood_changed"] = typeof(MoodChanged),
        ["relationship_changed"] = typeof(RelationshipChanged),
        ["fact_established"] = typeof(FactEstablished),
        ["fact_learned"] = typeof(FactLearned),
        ["character_introduced"] = typeof(CharacterIntroduced),
        ["character_renamed"] = typeof(CharacterRenamed),
        ["location_introduced"] = typeof(LocationIntroduced),
        ["item_introduced"] = typeof(ItemIntroduced),
        ["item_moved"] = typeof(ItemMoved),
        ["item_renamed"] = typeof(ItemRenamed),
        ["item_status_changed"] = typeof(ItemStatusChanged),
        ["location_status_changed"] = typeof(LocationStatusChanged),
        ["item_revealed_as_character"] = typeof(ItemRevealedAsCharacter),
        ["item_lost"] = typeof(ItemLost),
    };

    private static readonly Dictionary<Type, string> TypeToKind =
        KindToType.ToDictionary(pair => pair.Value, pair => pair.Key);

    public override StateDelta? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Expected an object for a delta, found {root.ValueKind}.");
        }

        if (!TryFindDiscriminator(root, out string? kind))
        {
            throw new JsonException(
                $"Delta has no '{Discriminator}' property. Properties present: " +
                $"{string.Join(", ", root.EnumerateObject().Select(p => p.Name))}.");
        }

        if (!KindToType.TryGetValue(kind, out Type? target))
        {
            // A kind outside the closed set. The schema should prevent it, but a provider
            // that ignores response_format would not, and a silent null here would look
            // exactly like the model choosing to report nothing.
            throw new JsonException(
                $"Unknown delta kind '{kind}'. Known kinds: {string.Join(", ", KindToType.Keys)}.");
        }

        return (StateDelta?)root.Deserialize(target, Inner(options));
    }

    public override void Write(Utf8JsonWriter writer, StateDelta value, JsonSerializerOptions options)
    {
        if (!TypeToKind.TryGetValue(value.GetType(), out string? kind))
        {
            throw new JsonException(
                $"No 'kind' registered for delta type '{value.GetType().Name}'. A delta was " +
                "added without extending StateDeltaConverter.");
        }

        // Written first for readability and so anything reading these files with the
        // built-in polymorphic support still works. Reading here does not depend on it.
        writer.WriteStartObject();
        writer.WriteString(Discriminator, kind);

        using JsonDocument document = JsonSerializer.SerializeToDocument(
            value, value.GetType(), Inner(options));

        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (!string.Equals(property.Name, Discriminator, StringComparison.OrdinalIgnoreCase))
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>Case-insensitive lookup, since property casing is no more guaranteed than
    /// property order.</summary>
    private static bool TryFindDiscriminator(JsonElement root, out string kind)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, Discriminator, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                kind = property.Value.GetString() ?? string.Empty;
                return kind.Length > 0;
            }
        }

        kind = string.Empty;
        return false;
    }

    /// <summary>
    /// Options with this converter removed, used to handle the concrete type. Without the
    /// removal, deserializing the derived type re-enters this converter and recurses forever.
    ///
    /// Cached per source options object. System.Text.Json builds and caches type metadata
    /// against an options instance, so constructing a fresh one per delta would discard that
    /// work on every single call.
    /// </summary>
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> InnerCache = new();

    private static JsonSerializerOptions Inner(JsonSerializerOptions options) =>
        InnerCache.GetValue(options, static source =>
        {
            JsonSerializerOptions inner = new(source);

            for (int i = inner.Converters.Count - 1; i >= 0; i--)
            {
                if (inner.Converters[i] is StateDeltaConverter)
                {
                    inner.Converters.RemoveAt(i);
                }
            }

            return inner;
        });
}
