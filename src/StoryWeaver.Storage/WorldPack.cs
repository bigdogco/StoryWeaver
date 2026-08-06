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
    public const string SheetDirectory = "characters";

    private WorldPack(
        string id,
        string directory,
        WorldState? seed,
        LoreBook lore,
        IReadOnlyDictionary<string, CharacterSheet> sheets)
    {
        Id = id;
        Directory = directory;
        Seed = seed;
        Lore = lore;
        Sheets = sheets;
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

    /// <summary>Authored identity, by character id. Empty for a pack that ships none.</summary>
    public IReadOnlyDictionary<string, CharacterSheet> Sheets { get; }

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

        WorldState? seed = LoadSeed(Path.Combine(directory, SeedFile));
        LoreBook lore = MarkdownLoreReader.Load(Path.Combine(directory, LoreDirectory));

        Dictionary<string, CharacterSheet> sheets = MarkdownSheetReader
            .Load(Path.Combine(directory, SheetDirectory))
            .ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);

        if (seed is not null)
        {
            ApplySheets(seed, sheets);
            RejectUnresolvedReferences(seed, lore, sheets, directory);
        }

        return new WorldPack(id, directory, seed, lore, sheets);
    }

    /// <summary>
    /// A <c>{{ }}</c> naming nothing fails the load, with the file and the id.
    ///
    /// It must never reach a prompt. An id in the prose is the bug that produced "the heavy oak
    /// door of the marrow-tavern flies outward" and forced the ForNarration / ForExtraction
    /// split, and a reference resolving to a blank is a sentence with a hole in it — both are
    /// worse than a startup that refuses to run and says why.
    ///
    /// Checked against the seed, which is the world as authored. A reference to somebody the
    /// story introduces later cannot be validated here, and that is the right trade: an author
    /// writes about the cast they wrote.
    /// </summary>
    private static void RejectUnresolvedReferences(
        WorldState seed,
        LoreBook lore,
        Dictionary<string, CharacterSheet> sheets,
        string directory)
    {
        foreach (CharacterSheet sheet in sheets.Values)
        {
            Check(sheet.Body, Path.Combine(directory, SheetDirectory, sheet.Id + ".md"));

            foreach ((string target, string phrase) in sheet.Attitudes)
            {
                Check(phrase, Path.Combine(directory, SheetDirectory, sheet.Id + ".md"));

                // An attitude toward nothing is a dangling edge with no visible symptom: the
                // sheet reads fine and the feeling attaches to nobody.
                if (seed.FindCharacter(target) is null && !lore.Contains(target))
                {
                    throw new InvalidDataException(
                        $"{Path.Combine(directory, SheetDirectory, sheet.Id + ".md")}: attitude " +
                        $"toward '{target}', which is neither a character in this world nor a " +
                        "lore entry.");
                }
            }
        }

        foreach (LoreEntry entry in lore.All)
        {
            Check(entry.Body, Path.Combine(directory, LoreDirectory, entry.Id + ".md"));
        }

        void Check(string text, string file)
        {
            foreach (string id in EntityReferences.Unresolved(text, seed))
            {
                throw new InvalidDataException(
                    $"{file}: {{{{{id}}}}} refers to nothing in this world.");
            }
        }
    }

    /// <summary>
    /// Merges authored identity into the starting world.
    ///
    /// <b>The sheet defines the character; the seed holds their starting state.</b> A sheet
    /// supplies name and description; the seed entry supplies location, mood, status, standing
    /// and knowledge. Nothing is written twice, so nothing can disagree — which is the failure
    /// every other arrangement shared in a different disguise.
    ///
    /// A sheet with no seed entry creates the character <b>offstage</b>: no location, which
    /// <see cref="Character.LocationId"/> already allows and <c>/character</c> already does. It
    /// lets an author write a cast before deciding where anybody stands.
    ///
    /// A seeded character with no sheet is untouched, so every pack that existed before sheets
    /// keeps working exactly as it did.
    /// </summary>
    private static void ApplySheets(WorldState seed, Dictionary<string, CharacterSheet> sheets)
    {
        foreach (CharacterSheet sheet in sheets.Values)
        {
            if (seed.FindCharacter(sheet.Id) is { } existing)
            {
                existing.Name = sheet.Name;
                existing.Description = sheet.Body;
                continue;
            }

            seed.Characters[sheet.Id] = new Character
            {
                Id = sheet.Id,
                Name = sheet.Name,
                Description = sheet.Body,
                LocationId = null,
            };
        }
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
