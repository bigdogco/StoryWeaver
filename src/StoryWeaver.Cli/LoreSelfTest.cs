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

        failures += CheckSeedMustHaveAPlayer();
        failures += CheckSheetMustBePlacedInTheSeed();
        failures += CheckSeededCharacterMustHaveALocation();
        failures += CheckSeededCharacterWithoutASheetIsUntouched();
        failures += CheckIdShape();
        failures += CheckMalformedSheetFilenameIsRefused();
        failures += CheckShippedPackLoads();
        failures += CheckAPlayerSheetReplacesCharacterCreation();
        failures += CheckTheSheetOwnsTheName();
        failures += CheckASeedNamingASheetedCharacterIsRefused();
        failures += CheckABlankNameIsRefused();
        failures += CheckPlayerSheetCannotDeclareAttitudes();
        failures += CheckSheetParsesWithNestedAttitudes();
        failures += CheckSheetRejectsUnknownKey();
        failures += CheckPlayerReferenceResolvesToTheName();
        failures += CheckUnresolvedReferenceIsFound();
        failures += CheckEstablishedTurnMatchesTheTurn();
        failures += CheckSourceIntroducedInSameBatch();
        failures += CheckItemBecomesCharacterAndSpeaks();
        failures += CheckFactSourceMustExist();
        failures += CheckRivalClaimsAreNotDuplicates();
        failures += CheckItemMustBeSomewhere();
        failures += CheckItemCannotBeBoth();
        failures += CheckItemMoveSwapsPlacement();
        failures += CheckItemIdIsUniqueAcrossNamespaces();
        failures += CheckSeedRoundTrip();
        failures += CheckSeedForcesTurnZero();
        failures += CheckMissingPackIsEmpty();
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
    /// A seeded world with nobody for the player to be must fail the load.
    ///
    /// Before this it started silently: no character creation, no error, and the narrator told
    /// "the player is nowhere yet" on every turn.
    /// </summary>
    private static int CheckSeedMustHaveAPlayer()
    {
        string root = Path.Combine(Path.GetTempPath(), "sw-noplayer-" + Guid.NewGuid().ToString("N"));
        string pack = Path.Combine(root, "marrow");
        Directory.CreateDirectory(pack);

        try
        {
            WorldState world = WorldSeeds.Marrow();
            world.Characters.Remove(Character.PlayerId);
            WorldPackWriter.WriteSeed(Path.Combine(pack, WorldPack.SeedFile), world);

            try
            {
                WorldPack.Load(root, "marrow");
            }
            catch (InvalidDataException)
            {
                Console.WriteLine("  ok    a seed with no player fails the load");
                return 0;
            }

            Console.WriteLine("  FAIL  a seed with no player loaded without complaint.");
            return 1;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A sheet with no seed entry must fail the load.
    ///
    /// It used to create the character offstage, and that character was unreachable rather than
    /// dormant: the narrator only sees the player's room, mention never moves anyone (0/7,
    /// across 21 runs), and <c>/character</c> refuses an id already in canon. The pack loaded
    /// and the character could never appear.
    /// </summary>
    private static int CheckSheetMustBePlacedInTheSeed()
    {
        return InPack("unplaced", pack =>
        {
            WorldPackWriter.WriteSeed(Path.Combine(pack, WorldPack.SeedFile), WorldSeeds.Marrow());

            Directory.CreateDirectory(Path.Combine(pack, WorldPack.SheetDirectory));
            File.WriteAllText(
                Path.Combine(pack, WorldPack.SheetDirectory, "warrior-mike.md"),
                "# Mike\n\nA warrior nobody remembered to seat.");

            return "a sheet with no seed entry fails the load";
        });
    }

    /// <summary>
    /// The same rule, reached the other way: a seed entry with no <c>locationId</c>. Separate
    /// from the sheet case because only that one can say "the sheet exists, the seat does not".
    /// </summary>
    private static int CheckSeededCharacterMustHaveALocation()
    {
        return InPack("nowhere", pack =>
        {
            WorldState world = WorldSeeds.Marrow();
            world.Characters["innkeeper-hald"].LocationId = null;
            WorldPackWriter.WriteSeed(Path.Combine(pack, WorldPack.SeedFile), world);

            return "a seeded character with no location fails the load";
        });
    }

    /// <summary>
    /// The other half of the reversal, and the one a regression would hit silently: tightening
    /// the sheet rule must not have made a sheet mandatory. Every pack authored before sheets
    /// existed is characters in <c>seed.json</c> and nothing else.
    /// </summary>
    private static int CheckSeededCharacterWithoutASheetIsUntouched()
    {
        string root = Path.Combine(Path.GetTempPath(), "sw-nosheet-" + Guid.NewGuid().ToString("N"));
        string pack = Path.Combine(root, "marrow");
        Directory.CreateDirectory(pack);

        try
        {
            WorldPackWriter.WriteSeed(Path.Combine(pack, WorldPack.SeedFile), WorldSeeds.Marrow());

            WorldPack loaded = WorldPack.Load(root, "marrow");

            if (loaded.Seed?.FindCharacter("innkeeper-hald") is { } hald && hald.Name == "Hald")
            {
                Console.WriteLine("  ok    a seeded character with no sheet still loads");
                return 0;
            }

            Console.WriteLine("  FAIL  a sheetless pack no longer loads its own characters.");
            return 1;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Ids are matched by exact string comparison everywhere, so <c>warrior_mike</c> and
    /// <c>warrior-mike</c> are two different things that read as one.
    /// </summary>
    private static int CheckIdShape()
    {
        (string Id, bool Ok)[] cases =
        [
            ("warrior-mike", true),
            ("player", true),
            ("marrow-tavern-2", true),
            ("warrior_mike", false),
            ("Warrior-Mike", false),
            ("warrior mike", false),
            ("-mike", false),
            ("mike-", false),
            ("warrior--mike", false),
            ("", false),
        ];

        int failures = 0;

        foreach ((string id, bool ok) in cases)
        {
            if (EntityId.IsWellFormed(id) != ok)
            {
                Console.WriteLine($"  FAIL  id '{id}' should have been {(ok ? "accepted" : "refused")}.");
                failures++;
            }
        }

        if (failures == 0)
        {
            Console.WriteLine("  ok    ids are lowercase words joined by single hyphens");
        }

        return failures;
    }

    /// <summary>
    /// The filename <i>is</i> the id, so the check has to bite at pack load and not only in a
    /// unit test — an underscore in a filename is the way this mistake actually arrives.
    /// </summary>
    private static int CheckMalformedSheetFilenameIsRefused()
    {
        return InPack("badid", pack =>
        {
            WorldPackWriter.WriteSeed(Path.Combine(pack, WorldPack.SeedFile), WorldSeeds.Marrow());

            Directory.CreateDirectory(Path.Combine(pack, WorldPack.SheetDirectory));
            File.WriteAllText(
                Path.Combine(pack, WorldPack.SheetDirectory, "warrior_mike.md"),
                "# Mike\n\nA warrior whose id has an underscore in it.");

            return "a sheet filename that is not a usable id fails the load";
        });
    }

    /// <summary>
    /// A pack that ships <c>characters/player.md</c> has decided who the player is, and the
    /// opening prompts must not run.
    ///
    /// Both branches, because the interesting failure is the one that still looks like it
    /// works: the sheet loads, the prompts run afterwards, and the pack's premise is overwritten
    /// by whatever the player typed. Checking only the sheet branch would pass while the
    /// blank-slate path silently stopped asking anyone their name.
    ///
    /// The prompts themselves are console input and cannot be driven from here. What is
    /// checked is the decision they hang off, and that the sheet actually reached the player's
    /// record.
    /// </summary>
    private static int CheckAPlayerSheetReplacesCharacterCreation()
    {
        string root = Path.Combine(Path.GetTempPath(), "sw-playersheet-" + Guid.NewGuid().ToString("N"));
        string pack = Path.Combine(root, "marrow");
        Directory.CreateDirectory(pack);

        try
        {
            WorldPackWriter.WriteSeed(Path.Combine(pack, WorldPack.SeedFile), WorldSeeds.Marrow());

            WorldPack blank = WorldPack.Load(root, "marrow");

            if (blank.AuthorsThePlayer)
            {
                Console.WriteLine("  FAIL  a pack with no player.md claimed to author the player.");
                return 1;
            }

            // Rewritten without the player's name: once a sheet exists it owns the name, and
            // a seed that also states one is a load error.
            WorldState authoredSeed = WorldSeeds.Marrow();
            authoredSeed.Characters[Character.PlayerId].Name = string.Empty;
            WorldPackWriter.WriteSeed(Path.Combine(pack, WorldPack.SeedFile), authoredSeed);

            Directory.CreateDirectory(Path.Combine(pack, WorldPack.SheetDirectory));
            File.WriteAllText(
                Path.Combine(pack, WorldPack.SheetDirectory, "player.md"),
                "# Aldric\n\nYou carry the crown's seal, and the authority that comes with it.");

            WorldPack authored = WorldPack.Load(root, "marrow");

            if (!authored.AuthorsThePlayer)
            {
                Console.WriteLine("  FAIL  a pack with player.md did not claim to author the player.");
                return 1;
            }

            if (authored.Seed?.Player is not { } player
                || player.Name != "Aldric"
                || !player.Description.Contains("crown's seal"))
            {
                Console.WriteLine("  FAIL  player.md did not reach the player's record.");
                return 1;
            }

            Console.WriteLine("  ok    a player sheet replaces character creation, and only then");
            return 0;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A character named only by their sheet loads, with the sheet's name.
    ///
    /// This is decision 1 working for the first time. Until 2026-08-12 the seed *could not*
    /// omit a name — <see cref="Entity.Name"/> was <c>required</c>, so the deserializer refused
    /// the file and every pack wrote the name twice while the design said it should not.
    /// </summary>
    private static int CheckTheSheetOwnsTheName()
    {
        string root = Path.Combine(Path.GetTempPath(), "sw-namedby-" + Guid.NewGuid().ToString("N"));
        string pack = Path.Combine(root, "marrow");
        Directory.CreateDirectory(Path.Combine(pack, WorldPack.SheetDirectory));

        try
        {
            WorldState world = WorldSeeds.Marrow();
            world.Characters["innkeeper-hald"].Name = string.Empty;
            WorldPackWriter.WriteSeed(Path.Combine(pack, WorldPack.SeedFile), world);

            File.WriteAllText(
                Path.Combine(pack, WorldPack.SheetDirectory, "innkeeper-hald.md"),
                "# Halden\n\nHeavyset and watchful, with a publican's memory for faces.");

            WorldPack loaded = WorldPack.Load(root, "marrow");

            if (loaded.Seed?.FindCharacter("innkeeper-hald")?.Name != "Halden")
            {
                Console.WriteLine("  FAIL  a character named only by their sheet did not get the name.");
                return 1;
            }

            Console.WriteLine("  ok    a sheet can be the only place a character is named");
            return 0;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Naming somebody in both files is refused, rather than silently resolved in the sheet's
    /// favour.
    ///
    /// The failure it prevents is a rename: change the name in the sheet, and the seed goes on
    /// asserting the old one where nothing reads it. Two files claiming one field is the shape
    /// decision 1 exists to forbid, and it had been the shape the format required.
    /// </summary>
    private static int CheckASeedNamingASheetedCharacterIsRefused()
    {
        return InPack("dupname", pack =>
        {
            // WorldSeeds.Marrow() names Hald, which is exactly the duplication under test.
            WorldPackWriter.WriteSeed(Path.Combine(pack, WorldPack.SeedFile), WorldSeeds.Marrow());

            Directory.CreateDirectory(Path.Combine(pack, WorldPack.SheetDirectory));
            File.WriteAllText(
                Path.Combine(pack, WorldPack.SheetDirectory, "innkeeper-hald.md"),
                "# Hald\n\nHeavyset and watchful.");

            return "a seed naming a character who has a sheet fails the load";
        });
    }

    /// <summary>
    /// A blank name is refused — which <c>required</c> never did. It checked that the property
    /// was present, and <c>"name": ""</c> is present.
    /// </summary>
    private static int CheckABlankNameIsRefused()
    {
        return InPack("blankname", pack =>
        {
            WorldState world = WorldSeeds.Marrow();
            world.Characters["drinker-mabb"].Name = "   ";
            WorldPackWriter.WriteSeed(Path.Combine(pack, WorldPack.SeedFile), world);

            return "a seeded character with a blank name fails the load";
        });
    }

    /// <summary>
    /// The pack this repository actually ships, loaded through the real path.
    ///
    /// Every other check here builds a pack to fail. This one exists because a load-time rule
    /// is a rule about content that already exists, and tightening one can break the world in
    /// the next folder over without a single synthetic test noticing.
    ///
    /// Skipped rather than failed when the folder is absent: the harness has always been
    /// runnable from anywhere, and <c>worlds/</c> resolves relative to the working directory.
    /// </summary>
    private static int CheckShippedPackLoads()
    {
        const string root = "worlds";

        if (!Directory.Exists(root))
        {
            Console.WriteLine($"  skip  no {root}/ in the working directory");
            return 0;
        }

        // Every folder, not a named one. A second pack is worthless as a regression guard if
        // the check only ever opens the first, and "the world in the next folder over" is
        // precisely the thing this exists to catch.
        string[] ids =
        [
            .. Directory.EnumerateDirectories(root)
                .Select(Path.GetFileName)
                .OfType<string>()
                .OrderBy(n => n, StringComparer.Ordinal),
        ];

        if (ids.Length == 0)
        {
            Console.WriteLine($"  skip  {root}/ has no packs in it");
            return 0;
        }

        int failures = 0;

        foreach (string id in ids)
        {
            try
            {
                WorldPack pack = WorldPack.Load(root, id);

                if (pack.Seed is null)
                {
                    Console.WriteLine($"  FAIL  {root}/{id} loaded without a seed.");
                    failures++;
                    continue;
                }

                string authored = pack.AuthorsThePlayer ? "authored player" : "blank slate";

                Console.WriteLine(
                    $"  ok    {root}/{id} loads — {pack.Seed.Characters.Count} seated, " +
                    $"{pack.Sheets.Count} with sheets, {pack.Lore.All.Count()} lore, {authored}");
            }
            catch (InvalidDataException ex)
            {
                Console.WriteLine($"  FAIL  {root}/{id} no longer loads: {ex.Message}");
                failures++;
            }
        }

        return failures;
    }

    /// <summary>
    /// Builds a throwaway pack, expects <see cref="WorldPack.Load"/> to refuse it, and cleans
    /// up either way. <paramref name="build"/> writes the pack and returns what passing means.
    /// </summary>
    private static int InPack(string label, Func<string, string> build)
    {
        string root = Path.Combine(Path.GetTempPath(), $"sw-{label}-" + Guid.NewGuid().ToString("N"));
        string pack = Path.Combine(root, "marrow");
        Directory.CreateDirectory(pack);

        try
        {
            string expectation = build(pack);

            try
            {
                WorldPack.Load(root, "marrow");
            }
            catch (InvalidDataException)
            {
                Console.WriteLine($"  ok    {expectation}");
                return 0;
            }

            Console.WriteLine($"  FAIL  expected a refusal: {expectation}.");
            return 1;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A pack may write the player's premise — "you carry the crown's seal" — but not how they
    /// feel about anyone. That is decided by playing.
    ///
    /// Refused rather than ignored. Player attitudes parse and validate, and are then never
    /// rendered, so without this an author gets a field that reads as working and does nothing:
    /// the silent drop this project refuses everywhere else.
    /// </summary>
    private static int CheckPlayerSheetCannotDeclareAttitudes()
    {
        string root = Path.Combine(Path.GetTempPath(), "sw-sheet-" + Guid.NewGuid().ToString("N"));
        string pack = Path.Combine(root, "marrow");
        Directory.CreateDirectory(Path.Combine(pack, WorldPack.SheetDirectory));

        try
        {
            // The name is cleared because the sheet owns it. Without this the pack fails for
            // duplication before it ever reaches the attitude check, and this test would pass
            // on the wrong exception — green, and testing nothing.
            WorldState seed = WorldSeeds.Marrow();
            seed.Characters[Character.PlayerId].Name = string.Empty;
            WorldPackWriter.WriteSeed(Path.Combine(pack, WorldPack.SeedFile), seed);

            File.WriteAllText(
                Path.Combine(pack, WorldPack.SheetDirectory, "player.md"),
                "---\nattitudes:\n  innkeeper-hald: distrusts him\n---\n\n# You\n\nA traveller.");

            try
            {
                WorldPack.Load(root, "marrow");
            }
            catch (InvalidDataException)
            {
                Console.WriteLine("  ok    a player sheet cannot declare attitudes");
                return 0;
            }

            Console.WriteLine("  FAIL  a player sheet declared attitudes and the pack loaded.");
            return 1;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// The one level of nesting the parser was extended for. Flattening attitudes to
    /// `dislikes: a, b` would have parsed with the reader that already existed and lost the
    /// phrase, which is the part the narrator would actually have used.
    /// </summary>
    private static int CheckSheetParsesWithNestedAttitudes()
    {
        CharacterSheet sheet = MarkdownSheetReader.Parse(
            """
            ---
            attitudes:
              kings-investigators: fears them, will not say the name aloud
              player: dislikes him — he stole his sword, years ago now
            ---

            # Hald

            Heavyset and watchful.
            """,
            "innkeeper-hald",
            "test");

        if (sheet.Name != "Hald"
            || sheet.Attitudes.Count != 2
            || sheet.Attitudes["player"] != "dislikes him — he stole his sword, years ago now")
        {
            Console.WriteLine("  FAIL  a sheet with nested attitudes did not parse as expected.");
            return 1;
        }

        Console.WriteLine("  ok    a sheet parses one level of nested attitudes");
        return 0;
    }

    /// <summary>
    /// Strictness survived the extension. A silently ignored typo means a sheet missing the
    /// field its author thought they wrote.
    /// </summary>
    private static int CheckSheetRejectsUnknownKey()
    {
        try
        {
            MarkdownSheetReader.Parse(
                "---\nattitudez: typo\n---\n\n# Hald\n\nHeavyset.",
                "innkeeper-hald",
                "test");
        }
        catch (InvalidDataException)
        {
            Console.WriteLine("  ok    a sheet refuses an unknown frontmatter key");
            return 0;
        }

        Console.WriteLine("  FAIL  a sheet accepted an unknown frontmatter key.");
        return 1;
    }

    /// <summary>
    /// `{{player}}` resolves to the name, not to "you" — a sheet describes a character rather
    /// than narrating to a reader, and extraction already holds the name in its roster.
    /// </summary>
    private static int CheckPlayerReferenceResolvesToTheName()
    {
        WorldState world = WorldSeeds.Marrow();
        world.Player!.Name = "Pavel";

        string resolved = EntityReferences.Resolve(
            "curious about {{player}}, and wary of {{innkeeper-hald}}",
            world);

        if (resolved != "curious about Pavel, and wary of Hald")
        {
            Console.WriteLine($"  FAIL  references resolved to '{resolved}'.");
            return 1;
        }

        Console.WriteLine("  ok    entity references resolve to current names");
        return 0;
    }

    /// <summary>
    /// A reference naming nothing must be catchable at load. Reaching a prompt it would render
    /// as a blank, and left unresolved it would put an id in the prose — the bug that forced
    /// the ForNarration / ForExtraction split.
    /// </summary>
    private static int CheckUnresolvedReferenceIsFound()
    {
        List<string> bad = [.. EntityReferences.Unresolved(
            "wary of {{nobody-at-all}} and of {{innkeeper-hald}}",
            WorldSeeds.Marrow())];

        if (bad.Count != 1 || bad[0] != "nobody-at-all")
        {
            Console.WriteLine($"  FAIL  unresolved references were {string.Join(", ", bad)}.");
            return 1;
        }

        Console.WriteLine("  ok    an unresolvable reference is found before it reaches a prompt");
        return 0;
    }

    /// <summary>
    /// A fact is stamped with the turn it happened on, and agrees with `LastSeenTurn`.
    ///
    /// These disagreed: deltas were applied before the counter moved, so a fact accepted on
    /// turn 7 recorded `establishedTurn: 6`, while presence was touched after the increment and
    /// was right. `EstablishedTurn` exists for ordering and for spotting facts invented late in
    /// a session, and an off-by-one makes it wrong for exactly that.
    /// </summary>
    private static int CheckEstablishedTurnMatchesTheTurn()
    {
        WorldState world = WorldSeeds.Marrow();
        world.TurnNumber = 6;

        // What the turn loop does, in order.
        world.TurnNumber++;
        DeltaApplier.Apply(world, [new FactEstablished("well-fluid", "The well weeps black fluid.")]);

        int stamped = world.FindFact("well-fluid")?.EstablishedTurn ?? -1;

        if (stamped != 7)
        {
            Console.WriteLine($"  FAIL  a fact established on turn 7 recorded turn {stamped}.");
            return 1;
        }

        Console.WriteLine("  ok    a fact is stamped with the turn it happened on");
        return 0;
    }

    /// <summary>
    /// A stranger walks in, says something, and both are recorded — the single commonest shape
    /// in play, and the one that broke when `source` was added without moving
    /// `FactEstablished` out of tier 0. The fact was judged before the speaker existed, and
    /// took every `fact_learned` down with it.
    /// </summary>
    private static int CheckSourceIntroducedInSameBatch()
    {
        WorldState world = WorldSeeds.Marrow();

        ValidationOutcome outcome = DeltaValidator.Validate(
            world,
            [
                new CharacterIntroduced("old-man", "An old man", "Weathered.", "marrow-tavern"),
                new FactEstablished("well-smells", "The well smells of copper.", "old-man"),
                new FactLearned(Character.PlayerId, "well-smells"),
            ]);

        if (outcome.Rejected.Count != 0 || outcome.Accepted.Count != 3)
        {
            Console.WriteLine(
                $"  FAIL  a stranger speaking in one batch produced {outcome.Rejected.Count} " +
                "rejection(s); expected none.");
            return 1;
        }

        Console.WriteLine("  ok    a character introduced and quoted in one batch is accepted");
        return 0;
    }

    /// <summary>
    /// An object proves to be a person, and speaks on the same turn.
    ///
    /// Both halves matter and the second is the one that silently breaks.
    /// <see cref="ItemRevealedAsCharacter"/> sits in tier 1 so a <see cref="FactEstablished"/>
    /// naming it as source — judged in tier 2 — sees a character that exists. Put it in the
    /// default tier and the fact is rejected for naming somebody who "does not exist", taking
    /// every <c>fact_learned</c> behind it. That is not hypothetical: it is what the missing
    /// promotion cost in play, and it is separately what mis-tiering <c>FactEstablished</c>
    /// cost when <c>source</c> was added — 16 of 23 rejections in one session.
    /// </summary>
    private static int CheckItemBecomesCharacterAndSpeaks()
    {
        WorldState world = WorldSeeds.Marrow();

        world.Items["tarp-covered-shape"] = new Item
        {
            Id = "tarp-covered-shape",
            Name = "Shape under a tarp",
            Description = "A shape under a heavy, salt-stained tarp.",
            LocationId = "marrow-tavern",
        };

        // Deliberately in the order a model would emit them, with the fact before the
        // promotion. Tier sorting is what makes this work; emission order must not matter.
        List<StateDelta> deltas =
        [
            new FactEstablished("the-debt", "The weeping silver was given to keep the debt.", "tarp-covered-shape"),
            new FactLearned(Character.PlayerId, "the-debt"),
            new ItemRevealedAsCharacter("tarp-covered-shape", "Bloated man", "A drowned man, still breathing."),
        ];

        ValidationOutcome outcome = DeltaValidator.Validate(world, deltas);

        if (outcome.Rejected.Count > 0)
        {
            Console.WriteLine(
                $"  FAIL  promotion batch rejected: {outcome.Rejected[0].Reason}");
            return 1;
        }

        DeltaApplier.Apply(world, outcome.Accepted);

        if (world.Items.ContainsKey("tarp-covered-shape"))
        {
            Console.WriteLine("  FAIL  the promoted item is still an item.");
            return 1;
        }

        if (world.FindCharacter("tarp-covered-shape") is not { } man
            || man.Name != "Bloated man"
            || man.LocationId != "marrow-tavern")
        {
            Console.WriteLine("  FAIL  the promoted character is missing, misnamed or nowhere.");
            return 1;
        }

        if (!man.Knows.Contains("the-debt"))
        {
            Console.WriteLine("  FAIL  the speaker does not know what they said.");
            return 1;
        }

        Console.WriteLine("  ok    an item can become a character and be quoted in one batch");
        return 0;
    }

    /// <summary>
    /// A source naming nobody is worse than no source: it reads as attributed while pointing
    /// at a character who does not exist.
    /// </summary>
    private static int CheckFactSourceMustExist()
    {
        ValidationOutcome outcome = DeltaValidator.Validate(
            WorldSeeds.Marrow(),
            [new FactEstablished("rumour", "The bridge is out.", "nobody-at-all")]);

        if (outcome.Rejected.Count != 1)
        {
            Console.WriteLine("  FAIL  a fact was attributed to a character who does not exist.");
            return 1;
        }

        Console.WriteLine("  ok    a fact's source must be a real character");
        return 0;
    }

    /// <summary>
    /// Two characters asserting the same thing are two claims, not a duplicate. Keying
    /// duplicate detection on the fact id alone would silently drop the second half of a
    /// disagreement — which is the case attribution exists for.
    /// </summary>
    private static int CheckRivalClaimsAreNotDuplicates()
    {
        WorldState world = WorldSeeds.Marrow();

        ValidationOutcome outcome = DeltaValidator.Validate(
            world,
            [
                new FactEstablished("stone-quarry", "The stone went to the quarry.", "innkeeper-hald"),
                new FactEstablished("stone-bog", "The stone went to the deep bog.", "drinker-mabb"),
            ]);

        if (outcome.Accepted.Count != 2)
        {
            Console.WriteLine($"  FAIL  rival claims collapsed to {outcome.Accepted.Count} accepted delta(s).");
            return 1;
        }

        DeltaApplier.Apply(world, outcome.Accepted);

        if (world.FindFact("stone-quarry")?.SourceId != "innkeeper-hald"
            || world.FindFact("stone-bog")?.SourceId != "drinker-mabb")
        {
            Console.WriteLine("  FAIL  attribution did not survive being applied.");
            return 1;
        }

        Console.WriteLine("  ok    contradictory claims are kept apart by their speakers");
        return 0;
    }

    /// <summary>
    /// An item that is neither placed nor held has silently stopped existing while still being
    /// in canon — the quietest way for an object to vanish.
    /// </summary>
    private static int CheckItemMustBeSomewhere()
    {
        ValidationOutcome outcome = DeltaValidator.Validate(
            WorldSeeds.Marrow(),
            [new ItemIntroduced("silver-pendant", "A silver pendant", "Tarnished.", null, null)]);

        if (outcome.Rejected.Count != 1)
        {
            Console.WriteLine("  FAIL  an item with no location and no holder was accepted.");
            return 1;
        }

        Console.WriteLine("  ok    an item must be placed or held");
        return 0;
    }

    /// <summary>
    /// Both fields set is how one object ends up recorded in two places, which is the shape of
    /// the merge that produced false canon in play.
    /// </summary>
    private static int CheckItemCannotBeBoth()
    {
        ValidationOutcome outcome = DeltaValidator.Validate(
            WorldSeeds.Marrow(),
            [new ItemIntroduced("silver-pendant", "A silver pendant", "Tarnished.", "marrow-tavern", "innkeeper-hald")]);

        if (outcome.Rejected.Count != 1)
        {
            Console.WriteLine("  FAIL  an item was accepted as both placed and held.");
            return 1;
        }

        Console.WriteLine("  ok    an item cannot be both placed and held");
        return 0;
    }

    /// <summary>
    /// Picking something up must clear where it was. Assigning only the new field would leave
    /// the old one set and the item in two places at once.
    /// </summary>
    private static int CheckItemMoveSwapsPlacement()
    {
        WorldState world = WorldSeeds.Marrow();

        ValidationOutcome introduced = DeltaValidator.Validate(
            world,
            [new ItemIntroduced("silver-pendant", "A silver pendant", "Tarnished.", "marrow-tavern", null)]);
        DeltaApplier.Apply(world, introduced.Accepted);

        ValidationOutcome moved = DeltaValidator.Validate(
            world,
            [new ItemMoved("silver-pendant", null, "innkeeper-hald")]);
        DeltaApplier.Apply(world, moved.Accepted);

        Item? item = world.FindItem("silver-pendant");

        if (item is null || item.HolderId != "innkeeper-hald" || item.LocationId is not null)
        {
            Console.WriteLine("  FAIL  picking an item up did not clear its location.");
            return 1;
        }

        if (world.ItemsIn("marrow-tavern").Any() || !world.ItemsHeldBy("innkeeper-hald").Any())
        {
            Console.WriteLine("  FAIL  the item is queryable from the wrong place after moving.");
            return 1;
        }

        Console.WriteLine("  ok    moving an item swaps placement rather than adding to it");
        return 0;
    }

    /// <summary>
    /// Ids are unique across all five namespaces, not merely within each. Found the hard way
    /// once already, when extraction reused a character id as a location.
    /// </summary>
    private static int CheckItemIdIsUniqueAcrossNamespaces()
    {
        WorldState world = WorldSeeds.Marrow();

        ValidationOutcome outcome = DeltaValidator.Validate(
            world,
            [new ItemIntroduced("innkeeper-hald", "A pendant", "Tarnished.", "marrow-tavern", null)]);

        if (outcome.Rejected.Count != 1)
        {
            Console.WriteLine("  FAIL  an item reused a character's id.");
            return 1;
        }

        ValidationOutcome other = DeltaValidator.Validate(
            world,
            [new LocationIntroduced("well-boarded", "Somewhere", "Nowhere in particular.")]);

        if (other.Rejected.Count != 1)
        {
            Console.WriteLine("  FAIL  a location reused a fact's id.");
            return 1;
        }

        Console.WriteLine("  ok    ids stay unique across every namespace");
        return 0;
    }

    /// <summary>
    /// A seed written and read back is the same world.
    ///
    /// The claim that made packs cheap is that a seed needs no format of its own — it is a
    /// <c>WorldState</c> at turn 0, using the options that already write canon. That claim is
    /// worth one test, because the failure mode is a mood or a relationship standing quietly
    /// not surviving the trip, which would look like a behaviour change in whatever got
    /// measured next.
    /// </summary>
    private static int CheckSeedRoundTrip()
    {
        string directory = Path.Combine(Path.GetTempPath(), "sw-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "marrow"));

        try
        {
            WorldState original = WorldSeeds.Marrow();
            WorldPackWriter.WriteSeed(Path.Combine(directory, "marrow", WorldPack.SeedFile), original);

            WorldState? read = WorldPack.Load(directory, "marrow").Seed;

            if (read is null)
            {
                Console.WriteLine("  FAIL  a written seed did not load back.");
                return 1;
            }

            Character? hald = read.FindCharacter("innkeeper-hald");

            bool same = read.Locations.Count == original.Locations.Count
                        && read.Characters.Count == original.Characters.Count
                        && read.Facts.Count == original.Facts.Count
                        && hald is not null
                        && hald.Mood == "guarded"
                        && hald.RelationshipToPlayer.Standing == -10
                        && hald.Knows.Contains("well-boarded")
                        // Case-insensitivity is carried by a converter and is exactly the sort
                        // of thing a round trip loses; it cost a bug once already.
                        && read.FindCharacter("INNKEEPER-HALD") is not null;

            if (!same)
            {
                Console.WriteLine("  FAIL  a seed did not survive the round trip intact.");
                return 1;
            }

            Console.WriteLine("  ok    a seed round-trips through the canon format");
            return 0;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// A save copied in as a seed starts a new world, not one that opens at turn 51. Copying
    /// a save is an entirely plausible way to author a pack.
    /// </summary>
    private static int CheckSeedForcesTurnZero()
    {
        string directory = Path.Combine(Path.GetTempPath(), "sw-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "marrow"));

        try
        {
            WorldState midStory = WorldSeeds.Marrow();
            midStory.TurnNumber = 51;

            WorldPackWriter.WriteSeed(Path.Combine(directory, "marrow", WorldPack.SeedFile), midStory);

            if (midStory.TurnNumber != 51)
            {
                Console.WriteLine("  FAIL  writing a seed mutated the world it was given.");
                return 1;
            }

            if (WorldPack.Load(directory, "marrow").Seed?.TurnNumber != 0)
            {
                Console.WriteLine("  FAIL  a seed did not start at turn 0.");
                return 1;
            }

            Console.WriteLine("  ok    a seed always starts at turn 0");
            return 0;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// A pack that is not there is an empty one. The harness has always been able to run with
    /// no content on disk and that stays true — a fresh clone must not need a pack to play.
    /// </summary>
    private static int CheckMissingPackIsEmpty()
    {
        WorldPack pack = WorldPack.Load(
            Path.Combine(Path.GetTempPath(), "sw-absent-" + Guid.NewGuid().ToString("N")),
            "nothing");

        if (pack.HasSeed || pack.Lore.Count != 0)
        {
            Console.WriteLine("  FAIL  a missing pack was not empty.");
            return 1;
        }

        Console.WriteLine("  ok    a missing pack loads as empty");
        return 0;
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
