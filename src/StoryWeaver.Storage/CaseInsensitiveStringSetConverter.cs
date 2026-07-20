using System.Text.Json;
using System.Text.Json.Serialization;

namespace StoryWeaver.Storage;

/// <summary>
/// Serializes <see cref="HashSet{String}"/> (the domain's <c>Knows</c> and <c>Connections</c>
/// sets) with two properties the default converter does not give us, and which matter for a
/// save file specifically:
///
/// <list type="bullet">
/// <item><b>Sorted on write.</b> A <see cref="HashSet{T}"/> makes no ordering guarantee, and
/// its enumeration order can change when it resizes — so adding one fact to a character's
/// knowledge could rewrite that whole set's block in the file. Sorting makes the diff show
/// only what actually changed.</item>
/// <item><b>Case-insensitive on read.</b> The domain builds these sets with
/// <see cref="StringComparer.OrdinalIgnoreCase"/>, but System.Text.Json constructs a fresh
/// default (case-sensitive) set when deserializing, silently dropping the comparer. A loaded
/// world would then compare ids case-sensitively while a seeded one does not.</item>
/// </list>
///
/// Save-only: this lives in Storage and is added to the save options, never to the
/// model-facing <c>StoryJson.Options</c>.
/// </summary>
internal sealed class CaseInsensitiveStringSetConverter : JsonConverter<HashSet<string>>
{
    public override HashSet<string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        HashSet<string> set = new(StringComparer.OrdinalIgnoreCase);

        if (reader.TokenType == JsonTokenType.Null)
        {
            return set;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected array for a string set, found {reader.TokenType}.");
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected string in set, found {reader.TokenType}.");
            }

            set.Add(reader.GetString()!);
        }

        return set;
    }

    public override void Write(
        Utf8JsonWriter writer,
        HashSet<string> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (string item in value.OrderBy(s => s, StringComparer.Ordinal))
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}
