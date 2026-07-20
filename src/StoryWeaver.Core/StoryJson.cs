using System.Text.Json;

namespace StoryWeaver.Core;

/// <summary>
/// The one JSON configuration for domain types.
///
/// Shared rather than constructed per call site so that <see cref="StateDeltaConverter"/>
/// cannot be forgotten somewhere — deltas that fail to deserialize look identical to a model
/// reporting no changes, which is a silent failure and took a live session to notice.
/// </summary>
public static class StoryJson
{
    /// <summary>For talking to models: compact, camelCase, order-independent deltas.</summary>
    public static readonly JsonSerializerOptions Options = Create(indented: false);

    /// <summary>For save files. Same shape, indented so a save is readable and
    /// <c>git diff</c> on it means something.</summary>
    public static readonly JsonSerializerOptions Pretty = Create(indented: true);

    private static JsonSerializerOptions Create(bool indented) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = indented,
        Converters = { new StateDeltaConverter() },
    };
}
