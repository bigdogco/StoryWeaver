using System.Text.Json;
using StoryWeaver.Core;

namespace StoryWeaver.Storage;

/// <summary>
/// JSON options for save files, built from Core's shared <see cref="StoryJson"/> so the two
/// never disagree about naming or the delta converter, then extended with the save-only
/// concerns that do not belong in the model-facing options.
/// </summary>
internal static class SaveJson
{
    /// <summary>
    /// Canon (<c>WorldState</c>). Indented like <see cref="StoryJson.Pretty"/> so a save reads
    /// well and diffs meaningfully, plus the sorting / case-insensitive collection converters.
    /// </summary>
    public static readonly JsonSerializerOptions Canon = BuildCanon();

    /// <summary>
    /// History (<c>TurnRecord</c>), one record per line. Compact on purpose — each turn is a
    /// single JSONL line, appended without rewriting the file. No collection converters are
    /// needed here: a turn record holds only scalars and lists, whose order is meaningful and
    /// must be preserved. Reuses the model-facing options directly for the delta converter.
    /// </summary>
    public static readonly JsonSerializerOptions History = StoryJson.Options;

    private static JsonSerializerOptions BuildCanon()
    {
        // Copy so we inherit camelCase and the StateDelta converter without mutating the
        // shared instance, then layer the save-only converters on.
        JsonSerializerOptions options = new(StoryJson.Pretty);
        options.Converters.Add(new CaseInsensitiveDictionaryConverter());
        options.Converters.Add(new CaseInsensitiveStringSetConverter());
        return options;
    }
}
