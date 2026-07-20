using System.Text.Json;
using System.Text.Json.Serialization;

namespace StoryWeaver.Storage;

/// <summary>
/// Serializes <c>Dictionary&lt;string, TValue&gt;</c> (the world's <c>Characters</c>,
/// <c>Locations</c>, and <c>Facts</c> maps) with sorted keys on write and a
/// case-insensitive comparer on read, for the same two reasons as
/// <see cref="CaseInsensitiveStringSetConverter"/>: a deterministic <c>git diff</c>, and
/// preserving the <see cref="StringComparer.OrdinalIgnoreCase"/> comparer that System.Text.Json
/// otherwise drops when it builds a fresh dictionary during deserialization.
///
/// A factory rather than a single closed type because the value differs per map
/// (<c>Character</c>, <c>Location</c>, <c>Fact</c>); the produced converter defers to the
/// serializer for values, so nested sets and objects keep their own converters.
///
/// Keys are written verbatim — they are entity ids (slugs), not names, and must not be run
/// through any naming policy.
/// </summary>
internal sealed class CaseInsensitiveDictionaryConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(Dictionary<,>)
        && typeToConvert.GetGenericArguments()[0] == typeof(string);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type valueType = typeToConvert.GetGenericArguments()[1];
        Type converterType = typeof(Inner<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class Inner<TValue> : JsonConverter<Dictionary<string, TValue>>
    {
        public override Dictionary<string, TValue> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            Dictionary<string, TValue> map = new(StringComparer.OrdinalIgnoreCase);

            if (reader.TokenType == JsonTokenType.Null)
            {
                return map;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected object for a map, found {reader.TokenType}.");
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string key = reader.GetString()!;
                reader.Read();
                TValue value = JsonSerializer.Deserialize<TValue>(ref reader, options)!;
                map[key] = value;
            }

            return map;
        }

        public override void Write(
            Utf8JsonWriter writer,
            Dictionary<string, TValue> value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (KeyValuePair<string, TValue> entry in value.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(entry.Key);
                JsonSerializer.Serialize(writer, entry.Value, options);
            }

            writer.WriteEndObject();
        }
    }
}
