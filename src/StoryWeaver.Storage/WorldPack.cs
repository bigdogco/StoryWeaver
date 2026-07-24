using System.Text.Json;
using StoryWeaver.Core;

namespace StoryWeaver.Storage;

/// <summary>
/// Everything an author writes, loaded from disk: the starting world and the lore.
///
/// <code>
/// worlds/marrow/
///   seed.json      the starting world — a WorldState at turn 0
///   lore/*.md      reference topics
/// </code>
///
/// <b>Content, not state.</b> A pack is authored, static, and shippable; a save is generated,
/// per-playthrough, and private. Keeping them apart is what lets a world be updated without
/// breaking saves, and shared without somebody's playthrough inside it — which is precisely
/// what the character-card ecosystem cannot do.
///
/// <b>The seed needs no format of its own.</b> It is a <see cref="WorldState"/> with
/// <c>turnNumber: 0</c>, read with the same <see cref="SaveJson.Canon"/> options that write
/// canon, so the two can never drift. A save file is a valid seed, which makes "start a new
/// world from where that one got to" free.
/// </summary>
public sealed class WorldPack
{
    public const string SeedFile = "seed.json";
    public const string LoreDirectory = "lore";

    private WorldPack(string id, string directory, WorldState? seed, LoreBook lore)
    {
        Id = id;
        Directory = directory;
        Seed = seed;
        Lore = lore;
    }

    /// <summary>Pack id — the folder name, and the default save id.</summary>
    public string Id { get; }

    public string Directory { get; }

    /// <summary>
    /// The starting world, or null when the pack ships none.
    ///
    /// Null is legitimate: a pack may be lore-only, and the harness falls back to its built-in
    /// seed. What is *not* legitimate is a seed file that exists and cannot be read — that
    /// throws, because an author who wrote a seed and got the built-in one instead would have
    /// no way to tell.
    /// </summary>
    public WorldState? Seed { get; }

    public LoreBook Lore { get; }

    public bool HasSeed => Seed is not null;

    /// <summary>
    /// Loads the pack at <paramref name="root"/>/<paramref name="id"/>.
    ///
    /// A missing pack is an empty one rather than an error — the harness has always been able
    /// to run with no content on disk, and that stays true.
    /// </summary>
    public static WorldPack Load(string root, string id)
    {
        string directory = Path.Combine(root, id);

        return new WorldPack(
            id,
            directory,
            LoadSeed(Path.Combine(directory, SeedFile)),
            MarkdownLoreReader.Load(Path.Combine(directory, LoreDirectory)));
    }

    private static WorldState? LoadSeed(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        WorldState? seed;

        try
        {
            seed = JsonSerializer.Deserialize<WorldState>(File.ReadAllText(path), SaveJson.Canon);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{path}: {ex.Message}");
        }

        if (seed is null)
        {
            throw new InvalidDataException($"{path}: the seed is empty.");
        }

        // A seed describing a player who is nowhere would open the story with "the player is
        // nowhere yet", which is a confusing way to discover a typo in a location id.
        if (seed.Player is { } player && player.LocationId is { } where && !seed.Locations.ContainsKey(where))
        {
            throw new InvalidDataException(
                $"{path}: the player starts in '{where}', which is not a location in this seed.");
        }

        // Turn 0 is what makes this a seed rather than a save. Enforced rather than assumed,
        // since a copied save file is an entirely plausible way to author one and would
        // otherwise start a "new" world mid-story with its turn counter intact.
        seed.TurnNumber = 0;

        return seed;
    }
}
