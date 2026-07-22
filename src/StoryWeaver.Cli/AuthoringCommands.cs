using StoryWeaver.Core;

namespace StoryWeaver.Cli;

/// <summary>
/// Player-authored canon: <c>/place</c>, <c>/character</c>, <c>/fact</c>.
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
        IWorldRepository repository)
    {
        Func<WorldState, IReadOnlyList<StateDelta>?>? prompt = input.ToLowerInvariant() switch
        {
            "/place" => PromptPlace,
            "/character" => PromptCharacter,
            "/fact" => PromptFact,
            _ => null,
        };

        if (prompt is null)
        {
            return false;
        }

        IReadOnlyList<StateDelta>? deltas = prompt(world);

        if (deltas is null)
        {
            Console.WriteLine("  Cancelled.");
            return true;
        }

        await CommitAsync(deltas, worldId, world, repository).ConfigureAwait(false);
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

    private static async Task CommitAsync(
        IReadOnlyList<StateDelta> deltas,
        string worldId,
        WorldState world,
        IWorldRepository repository)
    {
        ValidationOutcome validation = DeltaValidator.Validate(world, deltas);

        foreach (RejectedDelta rejected in validation.Rejected)
        {
            Console.WriteLine($"  REJECTED: {rejected.Reason}");
        }

        if (validation.Accepted.Count == 0)
        {
            Console.WriteLine("  Nothing was added.");
            return;
        }

        DeltaApplier.Apply(world, validation.Accepted);
        await repository.SaveAsync(worldId, world).ConfigureAwait(false);

        foreach (StateDelta delta in validation.Accepted)
        {
            Console.WriteLine($"  added: {Summarize(delta)}");
        }

        Console.WriteLine("  Saved to canon.");
    }

    private static string Summarize(StateDelta delta) => delta switch
    {
        LocationIntroduced d => $"place {d.LocationId} ({d.Name})",
        CharacterIntroduced d => $"character {d.CharacterId} ({d.Name})"
                                 + (d.LocationId is null ? " — offstage" : $" @ {d.LocationId}"),
        FactEstablished d => $"fact {d.FactId}: {d.Text}",
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
