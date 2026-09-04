using StoryWeaver.Core;

namespace StoryWeaver.Cli;

/// <summary>
/// The console's authoring commands: <c>/place</c>, <c>/character</c>, <c>/fact</c>,
/// <c>/rename</c>, <c>/knows</c>.
///
/// <b>Prompting and printing only.</b> What an id may be, when one collides, which deltas an
/// authoring act produces and what committing them does all live in <see cref="Authoring"/>,
/// because a UI needs the same answers and two copies of them would drift. This file owns the
/// conversation — the order questions are asked in, what is listed before each one, how a
/// rejection reads on a terminal — and nothing else.
///
/// The split is the point: a form with three text boxes asks these questions in no particular
/// order and calls exactly the same builders.
/// </summary>
internal static class AuthoringCommands
{
    /// <summary>Handles the command if it is one of ours. Returns false if it is not.</summary>
    public static async Task<bool> TryHandleAsync(
        string input,
        StorySession session,
        LoreBook? lore = null)
    {
        LoreBook book = lore ?? LoreBook.Empty;
        WorldState world = session.World;

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

        await CommitAsync(deltas, session).ConfigureAwait(false);
        return true;
    }

    private static IReadOnlyList<StateDelta>? PromptPlace(WorldState world)
    {
        string? name = ConsolePrompt.Ask("Name");
        if (name is null)
        {
            return null;
        }

        string? id = AskId("Id", Authoring.Slug(name), world);
        if (id is null)
        {
            return null;
        }

        string? description = ConsolePrompt.Ask("Description");
        return description is null ? null : Authoring.Place(id, name, description);
    }

    private static IReadOnlyList<StateDelta>? PromptCharacter(WorldState world)
    {
        string? name = ConsolePrompt.Ask("Name");
        if (name is null)
        {
            return null;
        }

        string? id = AskId("Id", Authoring.Slug(name), world);
        if (id is null)
        {
            return null;
        }

        string? description = ConsolePrompt.Ask("Description");
        if (description is null)
        {
            return null;
        }

        Console.WriteLine($"  Known places: {string.Join(", ", world.Locations.Keys.OrderBy(k => k, StringComparer.Ordinal))}");
        string? locationId = ConsolePrompt.AskOptional("Location id (blank = unknown / offstage)");

        return Authoring.Person(id, name, description, locationId);
    }

    private static IReadOnlyList<StateDelta>? PromptFact(WorldState world)
    {
        Console.WriteLine("  A fact is one sentence that is either true or not, and that");
        Console.WriteLine("  characters can learn separately. \"Bill stole the grain.\"");

        string? text = ConsolePrompt.Ask("Fact");
        if (text is null)
        {
            return null;
        }

        string? id = AskId("Id", Authoring.Slug(text), world);
        if (id is null)
        {
            return null;
        }

        return Authoring.Fact(id, text, ConsolePrompt.AskYesNo("Does your character know this?", defaultYes: true));
    }

    private static IReadOnlyList<StateDelta>? PromptRename(WorldState world)
    {
        ListCharacters(world);

        string? id = ConsolePrompt.Ask("Character id");
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

        string? name = ConsolePrompt.Ask("New name");
        if (name is null)
        {
            return null;
        }

        string? description = ConsolePrompt.AskOptional("New description (blank = keep current)");

        return Authoring.Rename(id, name, description);
    }

    private static IReadOnlyList<StateDelta>? PromptKnows(WorldState world, LoreBook lore)
    {
        if (lore.Count == 0)
        {
            Console.WriteLine("  This world has no lore entries.");
            return null;
        }

        ListCharacters(world);

        string? characterId = ConsolePrompt.Ask("Character id");
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

        string? loreId = ConsolePrompt.Ask("Lore id");
        if (loreId is null)
        {
            return null;
        }

        if (!lore.Contains(loreId))
        {
            Console.WriteLine($"  No lore entry with id '{loreId}'.");
            return null;
        }

        return Authoring.Knows(characterId, loreId);
    }

    private static async Task CommitAsync(IReadOnlyList<StateDelta> deltas, StorySession session)
    {
        // Through the session rather than straight to Authoring: authoring is a write, and
        // every write goes through the one guard. Without this an authored delta could land
        // in the middle of a turn, which is the case the guard exists for.
        SessionResult<ValidationOutcome> result = await session
            .AuthorAsync(deltas)
            .ConfigureAwait(false);

        if (result.WasRefused)
        {
            Console.WriteLine($"  Cannot author right now: {result.RefusedBecause}.");
            return;
        }

        ValidationOutcome validation = result.Value!;

        foreach (RejectedDelta rejected in validation.Rejected)
        {
            Console.WriteLine($"  REJECTED: {rejected.Reason}");
        }

        foreach (StateDelta noOp in validation.NoOps)
        {
            Console.WriteLine($"  no change: {Authoring.Summarize(noOp)}");
        }

        if (validation.Accepted.Count == 0)
        {
            Console.WriteLine("  Canon is unchanged.");
            return;
        }

        foreach (StateDelta delta in validation.Accepted)
        {
            Console.WriteLine($"  {Authoring.Summarize(delta)}");
        }

        Console.WriteLine("  Saved to canon.");
    }

    private static void ListCharacters(WorldState world)
    {
        Console.WriteLine("  Characters:");

        foreach (Character existing in world.Characters.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            Console.WriteLine($"    {existing.Id} — {existing.Name}");
        }
    }

    /// <summary>
    /// Prompt for an id, offering a slug derived from what was just typed.
    ///
    /// Shown rather than silently generated because ids are permanent, appear in every prompt
    /// the model sees, and are what a human reads when debugging a save.
    ///
    /// The collision check is <see cref="Authoring.IdConflict"/> — asked here, while the author
    /// can still choose another, rather than left to the validator after they have typed a
    /// description and lost it.
    /// </summary>
    private static string? AskId(string label, string suggestion, WorldState world)
    {
        while (true)
        {
            Console.Write($"  {label} [{suggestion}]: ");
            string? typed = Console.ReadLine()?.Trim();
            string id = string.IsNullOrWhiteSpace(typed) ? suggestion : Authoring.Slug(typed);

            if (Authoring.IdConflict(world, id) is { } reason)
            {
                Console.WriteLine($"  {reason}");
                continue;
            }

            return id;
        }
    }
}
