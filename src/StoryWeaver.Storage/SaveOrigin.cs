using System.Text.Json;

namespace StoryWeaver.Storage;

/// <summary>
/// What a save was started against: which pack, at which version, and when.
///
/// <b>Written once, at world creation, and never rewritten.</b> It records where a playthrough
/// came *from*, which is a fact about the past — rewriting it on resume would quietly erase the
/// only evidence that the pack has moved since.
///
/// The problem it exists for, from the pack design: an author edits a world while somebody has
/// a save in progress. That is normal, not exceptional. Without this, a save that suddenly
/// references a location the pack no longer defines produces a confused world and no
/// explanation; with it, the session can say which version it was built on and which it is
/// looking at now.
///
/// <b>Reporting only, for now.</b> Acting on a mismatch — dropping references to content a pack
/// has removed — is the compatibility work the design calls "state degrades quietly and
/// loudly", and it wants its own measurement.
/// </summary>
public sealed record SaveOrigin
{
    public const string FileName = "save.json";

    public string PackId { get; init; } = string.Empty;

    /// <summary>
    /// The pack version this playthrough began on. Empty when the pack declared none, which is
    /// legal and simply means no comparison is possible later.
    /// </summary>
    public string PackVersion { get; init; } = string.Empty;

    public DateTime StartedUtc { get; init; }

    /// <summary>
    /// Reads the origin of an existing save, or null when there is none — every save created
    /// before this existed is in that position, and must keep loading.
    /// </summary>
    public static SaveOrigin? Read(string directory)
    {
        string path = Path.Combine(directory, FileName);

        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<SaveOrigin>(File.ReadAllText(path), SaveJson.Canon)
                : null;
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // An unreadable origin is treated as none. It is a note about provenance, and
            // refusing to open somebody's world over a damaged note would be the wrong trade.
            return null;
        }
    }

    /// <summary>
    /// Writes the origin if the save does not already have one. Existing saves keep the record
    /// they were created with.
    /// </summary>
    public static void WriteIfAbsent(string directory, string packId, string packVersion)
    {
        string path = Path.Combine(directory, FileName);

        if (File.Exists(path))
        {
            return;
        }

        SaveOrigin origin = new()
        {
            PackId = packId,
            PackVersion = packVersion,
            StartedUtc = DateTime.UtcNow,
        };

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(origin, SaveJson.Canon));
    }
}
