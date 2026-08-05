using StoryWeaver.Core;

namespace StoryWeaver.Cli;

/// <summary>
/// Player-authored canon: <c>/place</c>, <c>/character</c>, <c>/fact</c>, <c>/rename</c>,
/// <c>/knows</c>.
///
/// Extraction deliberately never records a merely *mentioned* entity — measured at 0/7 for a
/// place the player names, a person they name, and a place the narrator names in passing. The
/// dividing line is presence, not authorship, and that is the right rule: a character saying
/// something is not the same as it being true, or every boast and lie would enter canon.
///
/// This is the door that rule does not apply to. The player is the world's author, so their
/// assertion is authoritative in a way an NPC's speech is not.
///
/// <b>Everything goes through the ordinary delta path</b> — build a <see cref="StateDelta"/>,
/// run it through <see cref="DeltaValidator"/>, apply it with <see cref="DeltaApplier"/>, save.
/// Writing to canon directly would be less code and a second way for the world to change,
/// which is how two paths start disagreeing about ids, collisions, and what was persisted.
/// </summary>
internal static class AuthoringCommands
{
    /// <summary>Handles the command if it is one of ours. Returns false if it is not.</summary>
    public static async Task<bool> TryHandleAsync(
        string input,
        string worldId,
        WorldState world,
        IWorldRepository repository,
        LoreBook? lore = null)
    {
        LoreBook book = lore ?? LoreBook.Empty;

        Func<WorldState, LoreBook, IReadOnlyList<StateDelta>?>? prompt = input.ToLowerInvariant() switch
        {
            "/place" => static (w, _) => PromptPlace(w),
            "/character" => static (w, _) => PromptCharacter(w),
            "/fact" => static (w, _) => PromptFact(w),
            "/rename" => static (w, _) => PromptRename(w),
            "/knows" => PromptKnows,
            _ => null,
        };

        if (prompt is null)
        {
            return false;
        }

        IReadOnlyList<StateDelta>? deltas = prompt(world, book);

        if (deltas is null)
        {
            Console.WriteLine("  Cancelled.");
            return true;
        }

        await CommitAsync(deltas, worldId, world, repository, book).ConfigureAwait(false);
        return true;
    }

    private static IReadOnlyList<StateDelta>? PromptPlace(WorldState world)
    {
        string? name = Ask("Name");
        if (name is null)
        {
            return null;
        }

        string? id = AskId("Id", Slug(name), world);
        if (id is null)
        {
            return null;
        }

        string? description = Ask("Description");
        return description is null ? null : [new LocationIntroduced(id, name, description)];
    }

    private static IReadOnlyList<StateDelta>? PromptCharacter(WorldState world)
    {
        string? name = Ask("Name");
        if (name is null)
        {
            return null;
        }

        string? id = AskId("Id", Slug(name), world);
        if (id is null)
        {
            return null;
        }

        string? description = Ask("Description");
        if (description is null)
        {
            return null;
        }

        // Blank means offstage. A person you have only spoken about — a brother back home, a
        // name from a rumour — exists without being anywhere yet, and Character.LocationId is
        // nullable exactly for that. They get placed when they actually turn up.
        Console.WriteLine($"  Known places: {string.Join(", ", world.Locations.Keys.OrderBy(k => k, StringComparer.Ordinal))}");
        string? locationId = AskOptional("Location id (blank = unknown / offstage)");

        return [new CharacterIntroduced(id, name, description, locationId)];
    }

    private static IReadOnlyList<StateDelta>? PromptFact(WorldState world)
    {
        Console.WriteLine("  A fact is one sentence that is either true or not, and that");
        Console.WriteLine("  characters can learn separately. \"Bill stole the grain.\"");

        string? text = Ask("Fact");
        if (text is null)
        {
            return null;
        }

        string? id = AskId("Id", Slug(text), world);
        if (id is null)
        {
            return null;
        }

        List<StateDelta> deltas = [new FactEstablished(id, text)];

        // Establishing a fact says nothing about who knows it — that separation is the whole
        // point of the knowledge model. An author may well write down a truth their own
        // character has not discovered.
        if (AskYesNo("Does your character know this?", defaultYes: true))
        {
            deltas.Add(new FactLearned(Character.PlayerId, id));
        }

        return deltas;
    }

    /// <summary>
    /// Rename someone already in canon — the manual counterpart to the extractor's
    /// <see cref="CharacterRenamed"/>, and the repair tool for worlds played before it existed.
    ///
    /// The id is shown but not offered for editing. It is permanent by design, and a prompt
    /// that displays it without a way to change it is clearer than one that hides it: the
    /// player sees that <c>figure-in-cistern</c> is now Nessa, and that this is fine.
    /// </summary>
    private static IReadOnlyList<StateDelta>? PromptRename(WorldState world)
    {
        Console.WriteLine("  Characters:");

        foreach (Character existing in world.Characters.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            Console.WriteLine($"    {existing.Id} — {existing.Name}");
        }

        string? id = Ask("Character id");
        if (id is null)
        {
            return null;
        }

        if (world.FindCharacter(id) is not { } character)
        {
            Console.WriteLine($"  No character with id '{id}'.");
            return null;
        }

        Console.WriteLine($"  Renaming {character.Id} — the id stays as it is.");
        Console.WriteLine($"  Currently: {character.Name} — {character.Description}");

        string? name = Ask("New name");
        if (name is null)
        {
            return null;
        }

        // Blank keeps the existing description. A reveal often rewrites it — "a shivering
        // figure in rags" is no longer who she is once she has a name — but not always, and
        // making it mandatory would mean retyping a good description to change a name.
        string? description = AskOptional("New description (blank = keep current)");

        return [new CharacterRenamed(id, name, description)
        {
            Evidence = "Renamed by the player.",
        }];
    }

    /// <summary>
    /// Grant a character knowledge of a lore entry — "Hald has heard of the Investigators".
    ///
    /// The authoring counterpart to learning one in play. It emits <see cref="FactLearned"/>
    /// against a lore id, which works because facts and lore share one id namespace; that is
    /// the same property that saves the extractor from needing a delta kind of its own.
    ///
    /// A seeded world starts with nobody having heard of anything, which is correct but
    /// unusable — an author needs to say that the innkeeper knows what the cult is without
    /// waiting for a scene to establish it.
    /// </summary>
    private static IReadOnlyList<StateDelta>? PromptKnows(WorldState world, LoreBook lore)
    {
        if (lore.Count == 0)
        {
            Console.WriteLine("  This world has no lore entries.");
            return null;
        }

        Console.WriteLine("  Characters:");

        foreach (Character existing in world.Characters.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            Console.WriteLine($"    {existing.Id} — {existing.Name}");
        }

        string? characterId = Ask("Character id");
        if (characterId is null)
        {
            return null;
        }

        if (world.FindCharacter(characterId) is not { } character)
        {
            Console.WriteLine($"  No character with id '{characterId}'.");
            return null;
        }

        Console.WriteLine("  Lore:");

        foreach (LoreEntry entry in lore.All)
        {
            string mark = character.Knows.Contains(entry.Id) ? "already knows" : "has not heard";
            Console.WriteLine($"    {entry.Id} — {entry.Title} ({mark})");
        }

        string? loreId = Ask("Lore id");
        if (loreId is null)
        {
            return null;
        }

        if (!lore.Contains(loreId))
        {
            Console.WriteLine($"  No lore entry with id '{loreId}'.");
            return null;
        }

        return [new FactLearned(characterId, loreId) { Evidence = "Authored by the player." }];
    }

    private static async Task CommitAsync(
        IReadOnlyList<StateDelta> deltas,
        string worldId,
        WorldState world,
        IWorldRepository repository,
        LoreBook lore)
    {
        ValidationOutcome validation = DeltaValidator.Validate(world, deltas, lore, authored: true);

        foreach (RejectedDelta rejected in validation.Rejected)
        {
            Console.WriteLine($"  REJECTED: {rejected.Reason}");
        }

        foreach (StateDelta noOp in validation.NoOps)
        {
            Console.WriteLine($"  no change: {Summarize(noOp)}");
        }

        if (validation.Accepted.Count == 0)
        {
            Console.WriteLine("  Canon is unchanged.");
            return;
        }

        DeltaApplier.Apply(world, validation.Accepted);
        await repository.SaveAsync(worldId, world).ConfigureAwait(false);

        foreach (StateDelta delta in validation.Accepted)
        {
            Console.WriteLine($"  {Summarize(delta)}");
        }

        Console.WriteLine("  Saved to canon.");
    }

    private static string Summarize(StateDelta delta) => delta switch
    {
        LocationIntroduced d => $"added place {d.LocationId} ({d.Name})",
        CharacterIntroduced d => $"added character {d.CharacterId} ({d.Name})"
                                 + (d.LocationId is null ? " — offstage" : $" @ {d.LocationId}"),
        CharacterRenamed d => $"renamed {d.CharacterId} to {d.Name}"
                              + (d.Description is null ? string.Empty : ", description revised"),
        FactEstablished d => $"added fact {d.FactId}: {d.Text}",
        FactLearned d => $"{d.CharacterId} knows {d.FactId}",
        _ => delta.GetType().Name,
    };

    /// <summary>Prompt for required text. Null means the player gave up.</summary>
    private static string? Ask(string label)
    {
        Console.Write($"  {label} (blank to cancel): ");
        string? value = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? AskOptional(string label)
    {
        Console.Write($"  {label}: ");
        string? value = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Prompt for an id, offering a slug derived from what was just typed.
    ///
    /// Shown rather than silently generated because ids are permanent, appear in every prompt
    /// the model sees, and are what a human reads when debugging a save.
    /// </summary>
    private static string? AskId(string label, string suggestion, WorldState world)
    {
        while (true)
        {
            Console.Write($"  {label} [{suggestion}]: ");
            string? typed = Console.ReadLine()?.Trim();
            string id = string.IsNullOrWhiteSpace(typed) ? suggestion : Slug(typed);

            if (id.Length == 0)
            {
                Console.WriteLine("  An id is required.");
                continue;
            }

            // The validator would catch this, but saying so now lets them pick another id
            // instead of losing everything they have typed so far.
            if (world.Characters.ContainsKey(id) || world.Locations.ContainsKey(id) || world.Facts.ContainsKey(id))
            {
                Console.WriteLine($"  '{id}' is already used by something in this world. Choose another.");
                continue;
            }

            return id;
        }
    }

    private static bool AskYesNo(string label, bool defaultYes)
    {
        Console.Write($"  {label} [{(defaultYes ? "Y/n" : "y/N")}]: ");
        string? value = Console.ReadLine()?.Trim();

        return string.IsNullOrWhiteSpace(value)
            ? defaultYes
            : value.StartsWith('y') || value.StartsWith('Y');
    }

    /// <summary>
    /// Human-readable slug, matching the ids the rest of the world uses
    /// (<c>marrow-tavern</c>, <c>innkeeper-hald</c>) rather than GUIDs.
    /// </summary>
    private static string Slug(string text)
    {
        System.Text.StringBuilder builder = new(text.Length);
        bool lastWasDash = false;

        foreach (char c in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasDash = false;
            }
            else if (c is '\'' or '’')
            {
                // Dropped, not treated as a separator. Apostrophes sit *inside* words —
                // "King's Investigators" must not become "king-s-investigators", and fantasy
                // names are full of them.
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        // Facts are slugged from a whole sentence, which would otherwise produce an
        // unreadable id. Four words is enough to recognise one in a save file.
        string slug = builder.ToString().Trim('-');
        string[] words = slug.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= 4 ? slug : string.Join('-', words[..4]);
    }
}
