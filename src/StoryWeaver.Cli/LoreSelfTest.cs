using StoryWeaver.Core;
using StoryWeaver.Storage;

namespace StoryWeaver.Cli;

/// <summary>
/// Offline checks on the lore markdown parser and the rules that keep lore out of canon.
///
/// The parser is strict on purpose — an unknown frontmatter key is refused rather than
/// ignored — and strictness is only worth anything if it is verified. A parser that silently
/// accepts a typo produces an entry missing the field the author thought they wrote, which is
/// the quiet-failure shape this project keeps deciding not to have.
/// </summary>
internal static class LoreSelfTest
{
    public static int Run()
    {
        int failures = 0;

        failures += Parses(
            "full entry",
            """
            ---
            keys: investigator, king's men
            always: true
            priority: 10
            ---

            # The King's Investigators

            An order answering to the crown.
            """,
            e => e.Title == "The King's Investigators"
                 && e.Keys.Count == 2
                 && e.Keys[1] == "king's men"
                 && e.Always
                 && e.Priority == 10
                 && e.Body == "An order answering to the crown.");

        // Frontmatter is optional: the minimum viable entry is a heading and a paragraph.
        failures += Parses(
            "no frontmatter",
            """
            # A Topic

            Something true about the world.
            """,
            e => e.Title == "A Topic" && e.Keys.Count == 0 && !e.Always && e.Priority == 0);

        failures += Parses(
            "multi-paragraph body keeps its breaks",
            """
            # A Topic

            First paragraph.

            Second paragraph.
            """,
            e => e.Body.Contains("First paragraph.") && e.Body.Contains("\n\nSecond paragraph."));

        failures += Rejects("unknown frontmatter key", """
            ---
            keyz: typo
            ---

            # A Topic

            Body.
            """);

        failures += Rejects("unclosed frontmatter", """
            ---
            priority: 1

            # A Topic

            Body.
            """);

        failures += Rejects("no title", "Just a body with no heading.");

        failures += Rejects("title but no body", "# A Topic");

        failures += Rejects("non-numeric priority", """
            ---
            priority: high
            ---

            # A Topic

            Body.
            """);

        failures += Parses(
            "common flag",
            """
            ---
            common: yes
            ---

            # The Kingdom

            Everyone lives here.
            """,
            e => e.Common && !e.Always);

        failures += CheckLoreIsNotEstablishable();
        failures += CheckLoreCanBeLearned();
        failures += CheckCommonIsKnownByEveryone();
        failures += CheckCommonIsNotWrittenToCanon();

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "All lore checks passed."
            : $"{failures} lore check(s) FAILED.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// The rule that keeps "lore is authored, never extracted" true in practice rather than
    /// only in intent.
    /// </summary>
    private static int CheckLoreIsNotEstablishable()
    {
        WorldState world = WorldSeeds.Marrow();
        LoreBook lore = WorldSeeds.MarrowLore();

        ValidationOutcome outcome = DeltaValidator.Validate(
            world,
            [new FactEstablished("cult-of-the-blind", "The cult exists.")],
            lore);

        if (outcome.Rejected.Count == 1 && outcome.Accepted.Count == 0)
        {
            Console.WriteLine("  ok    a lore id cannot be established as a fact");
            return 0;
        }

        Console.WriteLine("  FAIL  a lore id was accepted as a fact_established.");
        return 1;
    }

    /// <summary>
    /// The saving that justifies one shared id namespace: learning lore reuses
    /// <see cref="FactLearned"/> rather than needing a delta kind of its own.
    /// </summary>
    private static int CheckLoreCanBeLearned()
    {
        WorldState world = WorldSeeds.Marrow();
        LoreBook lore = WorldSeeds.MarrowLore();

        ValidationOutcome outcome = DeltaValidator.Validate(
            world,
            [new FactLearned(Character.PlayerId, "cult-of-the-blind")],
            lore);

        if (outcome.Accepted.Count != 1)
        {
            Console.WriteLine("  FAIL  fact_learned against a lore id was not accepted.");
            return 1;
        }

        DeltaApplier.Apply(world, outcome.Accepted);

        if (world.Player?.Knows.Contains("cult-of-the-blind") == true)
        {
            Console.WriteLine("  ok    a character can learn a lore entry");
            return 0;
        }

        Console.WriteLine("  FAIL  learning a lore entry did not reach the character.");
        return 1;
    }

    /// <summary>
    /// A common entry is known by everyone without anybody being told, and a non-common one
    /// still is not. Both halves matter: a flag that made everything visible would be worse
    /// than no flag, because per-character knowledge is the premise of the feature.
    /// </summary>
    private static int CheckCommonIsKnownByEveryone()
    {
        WorldState world = WorldSeeds.Marrow();
        LoreBook lore = WorldSeeds.MarrowLore();
        Character mabb = world.Characters["drinker-mabb"];

        List<string> known = [.. lore.KnownBy(mabb).Select(e => e.Id)];

        if (!known.Contains("kingdom-of-vaska"))
        {
            Console.WriteLine("  FAIL  a common entry was not known by an untold character.");
            return 1;
        }

        if (known.Contains("cult-of-the-blind"))
        {
            Console.WriteLine("  FAIL  a non-common entry leaked to a character who was never told.");
            return 1;
        }

        Console.WriteLine("  ok    common lore is known by everyone, other lore is not");
        return 0;
    }

    /// <summary>
    /// The rule that keeps the pack authoritative: common knowledge is answered from the
    /// lorebook every time it is asked, never copied into a save. If it were stored, an
    /// author later setting the flag false would leave saves holding entries that look
    /// learned in play and are not.
    /// </summary>
    private static int CheckCommonIsNotWrittenToCanon()
    {
        WorldState world = WorldSeeds.Marrow();
        LoreBook lore = WorldSeeds.MarrowLore();
        Character mabb = world.Characters["drinker-mabb"];

        _ = lore.KnownBy(mabb).ToList();

        if (mabb.Knows.Contains("kingdom-of-vaska"))
        {
            Console.WriteLine("  FAIL  reading common lore wrote it into the character's canon.");
            return 1;
        }

        Console.WriteLine("  ok    common lore is derived, never stored");
        return 0;
    }

    private static int Parses(string name, string markdown, Func<LoreEntry, bool> holds)
    {
        try
        {
            LoreEntry entry = MarkdownLoreReader.Parse(markdown, "test-entry", name);

            if (!holds(entry))
            {
                Console.WriteLine($"  FAIL  {name}: parsed, but the result was not as expected.");
                return 1;
            }

            Console.WriteLine($"  ok    {name}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL  {name}: {ex.Message}");
            return 1;
        }
    }

    private static int Rejects(string name, string markdown)
    {
        try
        {
            MarkdownLoreReader.Parse(markdown, "test-entry", name);
            Console.WriteLine($"  FAIL  {name}: accepted, should have been refused.");
            return 1;
        }
        catch (InvalidDataException)
        {
            Console.WriteLine($"  ok    {name} refused");
            return 0;
        }
    }
}
