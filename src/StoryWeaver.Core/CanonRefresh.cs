using System.Text.Json;
using System.Text.Json.Nodes;

namespace StoryWeaver.Core;

/// <summary>
/// **Update State**: re-read canon from disk mid-session, say what changed, and report anything
/// structurally wrong with it.
///
/// <b>The bug this closes.</b> <c>PROJECT.md</c> §3 locks that the player owns their world and
/// may edit it directly. A running session did not honour that: it loads canon once and holds
/// it in memory, so editing the save in another window and then taking a turn ran on the stale
/// copy and wrote over the edit. Silent, and it destroyed exactly the repair the feature exists
/// to allow.
///
/// <b>Explicit, and deliberately nothing more.</b> No file watching, no merge, no
/// reconciliation. Those are how a tool starts fighting its author, and they trade a problem
/// that an obvious action solves for a class of problems nothing solves. Edit the file, ask for
/// this, keep playing.
///
/// <b>Reported, never refused.</b> <see cref="DeltaValidator"/> exists to be suspicious of a
/// cheap model that confidently invents things. A person editing their own canon does not need
/// to be argued with — they get told what looks wrong and decide for themselves.
/// </summary>
public static class CanonRefresh
{
    /// <summary>
    /// Read canon from disk and compare it with what the session is holding.
    ///
    /// <paramref name="current"/> is not modified. The caller swaps in
    /// <see cref="RefreshReport.World"/> when it decides to, which keeps a failed or empty read
    /// from half-replacing a live session's state.
    /// </summary>
    public static async Task<RefreshReport> ReadAsync(
        string saveId,
        WorldState current,
        IWorldRepository repository,
        LoreBook? lore = null,
        CancellationToken cancellationToken = default)
    {
        WorldState? loaded = await repository.LoadAsync(saveId, cancellationToken).ConfigureAwait(false);

        return loaded is null
            ? new RefreshReport(null, [], [])
            : new RefreshReport(loaded, Diff(current, loaded), Check(loaded, lore));
    }

    /// <summary>
    /// Everything structurally wrong with a world, in the order a reader would want it.
    ///
    /// Public on its own because §3 makes validation on-demand rather than a gate: an editor
    /// wants to run these over canon it is about to save, with no reload involved.
    /// </summary>
    public static IReadOnlyList<string> Check(WorldState world, LoreBook? lore = null)
    {
        LoreBook book = lore ?? LoreBook.Empty;
        List<string> warnings = [];

        if (world.Player is null)
        {
            warnings.Add($"there is no character with the reserved id '{Character.PlayerId}'");
        }

        // The hand-edit failure specifically: rename a key, miss the field inside, and the
        // entity is unreachable by its own id while still looking correct in the file.
        foreach ((string key, Character character) in world.Characters)
        {
            RequireKeyMatchesId(warnings, "character", key, character.Id);
        }

        foreach ((string key, Location location) in world.Locations)
        {
            RequireKeyMatchesId(warnings, "location", key, location.Id);
        }

        foreach ((string key, Item item) in world.Items)
        {
            RequireKeyMatchesId(warnings, "item", key, item.Id);
        }

        foreach ((string key, Fact fact) in world.Facts)
        {
            RequireKeyMatchesId(warnings, "fact", key, fact.Id);
        }

        foreach (Character character in Ordered(world.Characters.Values, c => c.Id))
        {
            // Null is legal and means offstage — a brother back home, a name from a rumour.
            // Only a location that names nothing is wrong.
            if (character.LocationId is { } where && !world.Locations.ContainsKey(where))
            {
                warnings.Add($"character '{character.Id}' is at '{where}', which is not a place");
            }

            foreach (string factId in character.Knows.OrderBy(f => f, StringComparer.Ordinal))
            {
                // Facts and lore share one id namespace — the property that lets a lore entry
                // be learned without a delta kind of its own. Checking world.Facts alone would
                // warn about every lore entry anyone has heard of.
                if (!world.Facts.ContainsKey(factId) && !book.Contains(factId))
                {
                    warnings.Add($"character '{character.Id}' knows '{factId}', which is neither a fact nor lore");
                }
            }
        }

        foreach (Location location in Ordered(world.Locations.Values, l => l.Id))
        {
            foreach (string connection in location.Connections.OrderBy(c => c, StringComparer.Ordinal))
            {
                if (!world.Locations.ContainsKey(connection))
                {
                    warnings.Add($"place '{location.Id}' connects to '{connection}', which is not a place");
                }
            }
        }

        foreach (Item item in Ordered(world.Items.Values, i => i.Id))
        {
            // "Nowhere" is how an object silently stops existing while still being in canon.
            if (!item.IsPlaced && !item.IsHeld)
            {
                warnings.Add($"item '{item.Id}' is neither held nor anywhere");
            }

            if (item.IsPlaced && item.IsHeld)
            {
                warnings.Add($"item '{item.Id}' is both held by '{item.HolderId}' and lying in '{item.LocationId}'");
            }

            if (item.LocationId is { } at && !world.Locations.ContainsKey(at))
            {
                warnings.Add($"item '{item.Id}' is in '{at}', which is not a place");
            }

            if (item.HolderId is { } holder && !world.Characters.ContainsKey(holder))
            {
                warnings.Add($"item '{item.Id}' is held by '{holder}', who does not exist");
            }
        }

        return warnings;
    }

    private static void RequireKeyMatchesId(List<string> warnings, string what, string key, string id)
    {
        if (!string.Equals(key, id, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"{what} filed under '{key}' calls itself '{id}' — it cannot be found by its own id");
        }
    }

    /// <summary>What changed between the session's copy and the file, one line per entity.</summary>
    private static IReadOnlyList<string> Diff(WorldState before, WorldState after)
    {
        List<string> changes = [];

        if (before.TurnNumber != after.TurnNumber)
        {
            changes.Add($"turn {before.TurnNumber} → {after.TurnNumber}");
        }

        DiffBucket(changes, "character", before.Characters, after.Characters);
        DiffBucket(changes, "place", before.Locations, after.Locations);
        DiffBucket(changes, "item", before.Items, after.Items);
        DiffBucket(changes, "fact", before.Facts, after.Facts);

        return changes;
    }

    private static void DiffBucket<T>(
        List<string> changes,
        string what,
        Dictionary<string, T> before,
        Dictionary<string, T> after)
    {
        foreach (string id in after.Keys.Except(before.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(k => k, StringComparer.Ordinal))
        {
            changes.Add($"added {what} {id}");
        }

        foreach (string id in before.Keys.Except(after.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(k => k, StringComparer.Ordinal))
        {
            changes.Add($"removed {what} {id}");
        }

        foreach (string id in before.Keys.Intersect(after.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!string.Equals(Canonical(before[id]), Canonical(after[id]), StringComparison.Ordinal))
            {
                changes.Add($"changed {what} {id}");
            }
        }
    }

    /// <summary>
    /// An entity as comparable text.
    ///
    /// <b>Serialized rather than compared field by field on purpose.</b> A hand-written
    /// comparison silently stops covering any field added after it was written — the diff would
    /// keep passing and quietly miss the new one, which is the failure mode this whole feature
    /// exists to prevent, one level up.
    ///
    /// String arrays are sorted first because <c>Knows</c> and <c>Connections</c> are sets:
    /// their order carries no meaning and varies between a set built by replaying deltas and
    /// the same set read back from a file, which would otherwise report every character as
    /// changed on every reload.
    /// </summary>
    private static string Canonical<T>(T entity)
    {
        JsonNode? node = JsonSerializer.SerializeToNode(entity, StoryJson.Options);
        SortStringArrays(node);
        return node?.ToJsonString() ?? "null";
    }

    private static void SortStringArrays(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (KeyValuePair<string, JsonNode?> property in obj)
                {
                    SortStringArrays(property.Value);
                }

                break;

            case JsonArray array:
                if (array.All(e => e is JsonValue value && value.TryGetValue(out string? _)))
                {
                    List<string> sorted = [.. array
                        .Select(e => e!.GetValue<string>())
                        .OrderBy(s => s, StringComparer.Ordinal)];

                    array.Clear();

                    foreach (string item in sorted)
                    {
                        array.Add(item);
                    }
                }
                else
                {
                    foreach (JsonNode? element in array)
                    {
                        SortStringArrays(element);
                    }
                }

                break;
        }
    }

    private static IEnumerable<T> Ordered<T>(IEnumerable<T> items, Func<T, string> id) =>
        items.OrderBy(id, StringComparer.Ordinal);
}

/// <summary>
/// What a re-read found. <paramref name="World"/> is null when the save does not exist on disk,
/// which is not an error — it is a session that has not saved yet.
/// </summary>
public sealed record RefreshReport(
    WorldState? World,
    IReadOnlyList<string> Changes,
    IReadOnlyList<string> Warnings)
{
    public bool NothingOnDisk => World is null;

    public bool Unchanged => World is not null && Changes.Count == 0;
}
