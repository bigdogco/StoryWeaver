using StoryWeaver.Core;

namespace StoryWeaver.Cli;

/// <summary>
/// <c>/edit</c> — the escape hatch, on the console.
///
/// <b>The second choice, on purpose.</b> Deltas are how canon changes; this is for the things
/// the seventeen delta kinds cannot say — rewriting a description, rewording a fact, making
/// somebody forget, removing something added by mistake. Anything a delta *can* express should
/// go through <c>/place</c>, <c>/character</c>, <c>/fact</c>, <c>/rename</c> or <c>/knows</c>,
/// where it is validated before it lands rather than checked afterwards.
///
/// <b>Prompting only.</b> What each edit actually does to canon — and what a removal drags with
/// it — lives in <see cref="CanonEdits"/>, because a window will offer the same edits and two
/// implementations of "remove a character" would forget different things.
///
/// <b>The warning appears on removal and nowhere else.</b> A warning on every edit is clicked
/// through by the third one, and then it is worse than nothing. Rewording a description corrupts
/// nothing; the risk is concentrated in ids and references, so that is where the console stops
/// and asks — with the actual consequences, computed from canon, rather than a general caution.
/// </summary>
internal static class EditCommand
{
    public static async Task<bool> TryHandleAsync(string input, StorySession session)
    {
        if (!string.Equals(input, "/edit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        WorldState world = session.World;

        Console.WriteLine();
        Console.WriteLine("  Direct edit — for what the ordinary commands cannot say.");
        Console.WriteLine("  Prefer /place /character /fact /rename /knows where they fit:");
        Console.WriteLine("  those are checked before they land, this is checked after.");
        Console.WriteLine();

        int? choice = ConsolePrompt.AskChoice(
            "What are you changing?",
            "a description — a place, a person or a thing",
            "the wording of a fact",
            "something a character knows — make them forget it",
            "remove something from canon");

        Action<WorldState>? edit = choice switch
        {
            0 => PromptDescribe(world),
            1 => PromptReword(world),
            2 => PromptForget(world),
            3 => PromptRemove(world),
            _ => null,
        };

        if (edit is null)
        {
            Console.WriteLine("  Cancelled. Canon is unchanged.");
            return true;
        }

        SessionResult<EditReport> result = await session.EditAsync(edit).ConfigureAwait(false);

        if (result.WasRefused)
        {
            Console.WriteLine($"  Cannot edit right now: {result.RefusedBecause}.");
            return true;
        }

        Console.WriteLine("  Saved to canon.");

        foreach (string warning in result.Value!.Warnings)
        {
            Console.WriteLine($"  CHECK: {warning}");
        }

        if (!result.Value.IsClean)
        {
            Console.WriteLine("  Reported, not refused — it is your world. /edit again to tidy up.");
        }

        return true;
    }

    private static Action<WorldState>? PromptDescribe(WorldState world)
    {
        Console.WriteLine("  Describing something is what it *is*, not what has happened to it.");

        string? id = AskExistingId(world, "Id of the place, person or thing");

        if (id is null)
        {
            return null;
        }

        Console.WriteLine($"  Currently: {Current(world, id)}");

        string? description = ConsolePrompt.Ask("New description");

        return description is null ? null : CanonEdits.Describe(id, description);
    }

    private static Action<WorldState>? PromptReword(WorldState world)
    {
        if (world.Facts.Count == 0)
        {
            Console.WriteLine("  This world has no facts yet.");
            return null;
        }

        foreach (Fact fact in world.Facts.Values.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            Console.WriteLine($"    {fact.Id} — {fact.Text}");
        }

        string? factId = ConsolePrompt.Ask("Fact id");

        if (factId is null)
        {
            return null;
        }

        if (!world.Facts.ContainsKey(factId))
        {
            Console.WriteLine($"  No fact with id '{factId}'.");
            return null;
        }

        // Worth saying, because it is the reassuring half: knowledge holds fact ids, never text,
        // so rewording cannot leave two characters knowing different versions of one fact.
        Console.WriteLine("  Rewording does not change who knows it.");

        string? text = ConsolePrompt.Ask("New wording");

        return text is null ? null : CanonEdits.RewordFact(factId, text);
    }

    private static Action<WorldState>? PromptForget(WorldState world)
    {
        string? characterId = AskCharacter(world);

        if (characterId is null)
        {
            return null;
        }

        Character character = world.FindCharacter(characterId)!;

        if (character.Knows.Count == 0)
        {
            Console.WriteLine($"  {character.Name} knows nothing yet.");
            return null;
        }

        Console.WriteLine($"  {character.Name} knows:");

        foreach (string known in character.Knows.OrderBy(k => k, StringComparer.Ordinal))
        {
            string text = world.Facts.TryGetValue(known, out Fact? fact) ? fact.Text : "(a lore entry)";
            Console.WriteLine($"    {known} — {text}");
        }

        string? factId = ConsolePrompt.Ask("Id to forget");

        if (factId is null)
        {
            return null;
        }

        if (!character.Knows.Contains(factId))
        {
            Console.WriteLine($"  {character.Name} does not know '{factId}'.");
            return null;
        }

        return CanonEdits.Forget(characterId, factId);
    }

    /// <summary>
    /// The only edit that stops and asks twice, and the only one that warns — because it is the
    /// only one that can leave canon referring to something that no longer exists.
    ///
    /// The consequences are computed from canon before anything happens, so the warning names
    /// the actual key that will be left on the floor rather than saying something might break.
    /// </summary>
    private static Action<WorldState>? PromptRemove(WorldState world)
    {
        string? id = AskExistingId(world, "Id to remove");

        if (id is null)
        {
            return null;
        }

        Console.WriteLine();
        Console.WriteLine($"  Removing {id} — {Current(world, id)}");

        IReadOnlyList<string> consequences = CanonEdits.ConsequencesOfRemoving(world, id);

        if (consequences.Count == 0)
        {
            Console.WriteLine("  Nothing else in canon refers to it.");
        }
        else
        {
            Console.WriteLine();

            foreach (string consequence in consequences)
            {
                Console.WriteLine($"  !  {consequence}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("  This cannot be undone — canon has no snapshot to go back to.");

        return ConsolePrompt.Confirm("remove") ? CanonEdits.Remove(id) : null;
    }

    private static string? AskExistingId(WorldState world, string label)
    {
        string? id = ConsolePrompt.Ask(label);

        if (id is null)
        {
            return null;
        }

        if (!CanonEdits.Exists(world, id))
        {
            Console.WriteLine($"  Nothing in this world is called '{id}'. /state lists the ids.");
            return null;
        }

        return id;
    }

    private static string? AskCharacter(WorldState world)
    {
        Console.WriteLine("  Characters:");

        foreach (Character existing in world.Characters.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            Console.WriteLine($"    {existing.Id} — {existing.Name}");
        }

        string? id = ConsolePrompt.Ask("Character id");

        if (id is null)
        {
            return null;
        }

        if (world.FindCharacter(id) is null)
        {
            Console.WriteLine($"  No character with id '{id}'.");
            return null;
        }

        return id;
    }

    /// <summary>One line saying what something currently is, so an edit shows what it replaces.</summary>
    private static string Current(WorldState world, string id)
    {
        if (world.FindCharacter(id) is { } character)
        {
            return $"{character.Name}: {Shorten(character.Description)}";
        }

        if (world.FindLocation(id) is { } location)
        {
            return $"{location.Name}: {Shorten(location.Description)}";
        }

        if (world.FindItem(id) is { } item)
        {
            return $"{item.Name}: {Shorten(item.Description)}";
        }

        return world.Facts.TryGetValue(id, out Fact? fact) ? fact.Text : "(unknown)";
    }

    private static string Shorten(string text)
    {
        string flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= 90 ? flat : flat[..87] + "...";
    }
}
