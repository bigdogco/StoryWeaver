using System.Diagnostics;

using StoryWeaver.Core;
using StoryWeaver.Llm;
using StoryWeaver.Llm.Configuration;
using StoryWeaver.Llm.Story;
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
        failures += CheckASaveOpenTwiceIsRefused();
        failures += CheckAStaleLockIsTaken();
        failures += CheckForceTakesALiveLock();
        failures += CheckReleasingMakesTheSaveAvailable();
        failures += CheckAPackWithNoScenarioStillLoads();
        failures += CheckScenarioReachesNarratorNotExtractor();
        failures += CheckScenarioSitsAboveTheVolatileBlock();
        failures += CheckScenarioReferencesResolveToNames();
        failures += CheckOpeningIsRememberedButNotRecorded();
        failures += CheckOpeningLeavesTheWindow();
        failures += CheckAPackWithNoManifestIsNamedAfterItsFolder();
        failures += CheckAManifestMustAgreeWithItsFolder();
        failures += CheckSaveOriginIsWrittenOnceAndNotRewritten();
        failures += CheckRepairSectionsAreReadable();
        failures += CheckAMissingPromptFileFailsLoudly();
        failures += CheckTheFingerprintFollowsThePrompts();
        failures += CheckAPackMayNotOverrideExtraction();
        failures += CheckAPackVoiceIsAddedNotSubstituted();
        failures += CheckWalkingSomewhereConnectsIt();
        failures += CheckAWalkedRouteIsTwoWay();
        failures += CheckItemBecomesCharacterAndSpeaks();
        failures += CheckALostItemLeavesCanon();
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
                string story = pack.HasScenario ? "has a scenario" : "no scenario";

                Console.WriteLine(
                    $"  ok    {root}/{id} loads — {pack.Seed.Characters.Count} seated, " +
                    $"{pack.Sheets.Count} with sheets, {pack.Lore.All.Count()} lore, {authored}, {story}");
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
    /// <summary>
    /// <b>Walking from one place to another records that the two are connected.</b>
    ///
    /// The failure this exists for, found in the 150-turn ashfall run: nothing in the delta
    /// set could ever connect two locations. <c>LocationIntroduced</c> carries no connections
    /// and no other delta touches the field, so every location extraction ever created was an
    /// orphan — 33 of them across nine saves, in both worlds.
    ///
    /// It reads as a narration failure and is not one. <c>ContextAssembler</c> renders
    /// "Leads to:" from this set, so the narrator was told the player stood in a sealed room
    /// and narrated exactly that, correctly, for seventy turns.
    /// </summary>
    /// <summary>
    /// <b>A save already open in a live session is refused.</b>
    ///
    /// The failure this exists for: two CLI instances played one save for a hundred turns,
    /// each overwriting the other's canon every turn, with no error anywhere — 72 duplicated
    /// turn numbers in a 250-turn log.
    ///
    /// Tested against a **real** child process rather than a forged lock file. The whole
    /// mechanism is "is that other session still running", and a test that fakes the other
    /// session is not testing the thing that failed.
    /// </summary>
    private static int CheckASaveOpenTwiceIsRefused()
    {
        using TempDirectory root = new();
        using Process? other = StartWaitingProcess();

        if (other is null)
        {
            Console.WriteLine("  ok    (skipped) no way to start a helper process on this platform");
            return 0;
        }

        try
        {
            Directory.CreateDirectory(Path.Combine(root.Path, "world"));

            File.WriteAllText(
                Path.Combine(root.Path, "world", SaveLock.FileName),
                $$"""
                {
                  "processId": {{other.Id}},
                  "startedUtc": "{{other.StartTime.ToUniversalTime():o}}",
                  "machine": "{{Environment.MachineName}}",
                  "openedUtc": "{{DateTime.UtcNow:o}}"
                }
                """);

            using SaveLock? refused = SaveLock.Acquire(root.Path, "world", force: false, out string? heldBy);

            if (refused is not null)
            {
                Console.WriteLine("  FAIL  a save held by a live session was opened anyway.");
                return 1;
            }

            if (heldBy is null || !heldBy.Contains(other.Id.ToString()))
            {
                Console.WriteLine($"  FAIL  the refusal did not name the holder (got: {heldBy ?? "null"}).");
                return 1;
            }

            Console.WriteLine("  ok    a save open in a live session is refused, and names the holder");
            return 0;
        }
        finally
        {
            try
            {
                other.Kill();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    /// <summary>
    /// A short-lived child process to stand in for a second session. Returns null rather than
    /// throwing on a platform with neither shell — a self-test that cannot run should skip,
    /// not fail.
    /// </summary>
    private static Process? StartWaitingProcess()
    {
        ProcessStartInfo info = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", "/c timeout /t 30 /nobreak")
            : new ProcessStartInfo("sleep", "30");

        info.CreateNoWindow = true;
        info.UseShellExecute = false;
        info.RedirectStandardOutput = true;

        try
        {
            return Process.Start(info);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// <b>A crash must never brick a save.</b> A lock whose process is gone is stale and taken
    /// silently — the alternative is a world nobody can open until they find and delete a file
    /// they were never told about.
    /// </summary>
    private static int CheckAStaleLockIsTaken()
    {
        using TempDirectory root = new();

        Directory.CreateDirectory(Path.Combine(root.Path, "world"));

        // A process id that cannot be running: ids are positive, so this can never match.
        File.WriteAllText(
            Path.Combine(root.Path, "world", SaveLock.FileName),
            """
            {
              "processId": 2147483646,
              "startedUtc": "2020-01-01T00:00:00Z",
              "machine": "MACHINE_NAME",
              "openedUtc": "2020-01-01T00:00:00Z"
            }
            """.Replace("MACHINE_NAME", Environment.MachineName));

        using SaveLock? taken = SaveLock.Acquire(root.Path, "world", force: false, out string? heldBy);

        if (taken is null)
        {
            Console.WriteLine($"  FAIL  a stale lock blocked the save (held by: {heldBy}).");
            return 1;
        }

        Console.WriteLine("  ok    a lock whose process is gone is taken");
        return 0;
    }

    /// <summary>
    /// <b>--force takes a lock held by a process that really is alive.</b> The escape hatch for
    /// the day the staleness check is wrong, and the only path that overrides a live holder.
    /// </summary>
    private static int CheckForceTakesALiveLock()
    {
        using TempDirectory root = new();

        Directory.CreateDirectory(Path.Combine(root.Path, "world"));

        // A live process that is not this one, described accurately enough to pass IsAlive.
        using Process self = Process.GetCurrentProcess();

        File.WriteAllText(
            Path.Combine(root.Path, "world", SaveLock.FileName),
            $$"""
            {
              "processId": {{Environment.ProcessId}},
              "startedUtc": "{{self.StartTime.ToUniversalTime():o}}",
              "machine": "{{Environment.MachineName}}",
              "openedUtc": "{{DateTime.UtcNow:o}}"
            }
            """);

        using SaveLock? forced = SaveLock.Acquire(root.Path, "world", force: true, out _);

        if (forced is null)
        {
            Console.WriteLine("  FAIL  --force did not take a live lock.");
            return 1;
        }

        Console.WriteLine("  ok    --force takes a live lock");
        return 0;
    }

    /// <summary>
    /// Ending a session gives the save back. Without this the guard would be worse than none:
    /// every clean quit would leave a world that only --force could reopen.
    /// </summary>
    private static int CheckReleasingMakesTheSaveAvailable()
    {
        using TempDirectory root = new();

        SaveLock? first = SaveLock.Acquire(root.Path, "world", force: false, out _);
        first?.Dispose();

        string lockPath = Path.Combine(root.Path, "world", SaveLock.FileName);

        if (File.Exists(lockPath))
        {
            Console.WriteLine("  FAIL  ending a session left its lock behind.");
            return 1;
        }

        using SaveLock? second = SaveLock.Acquire(root.Path, "world", force: false, out string? heldBy);

        if (second is null)
        {
            Console.WriteLine($"  FAIL  a released save would not reopen (held by: {heldBy}).");
            return 1;
        }

        Console.WriteLine("  ok    ending a session releases the save");
        return 0;
    }

    /// <summary>A scratch directory that removes itself, so lock tests never touch real saves.</summary>
    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "storyweaver-selftest-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// A pack that says nothing about its story loads and plays exactly as before. Every world
    /// shipped before 2026-08-15 is in this position, and breaking them to add a feature they
    /// do not use would be the wrong trade.
    /// </summary>
    private static int CheckAPackWithNoScenarioStillLoads()
    {
        string root = Path.Combine(Path.GetTempPath(), "sw-noscenario-" + Guid.NewGuid().ToString("N"));
        string pack = Path.Combine(root, "quiet");

        try
        {
            Directory.CreateDirectory(pack);
            File.WriteAllText(
                Path.Combine(pack, WorldPack.SeedFile),
                """
                {
                  "turnNumber": 0,
                  "locations": { "hall": { "id": "hall", "name": "hall", "description": "A hall." } },
                  "characters": {
                    "player": { "id": "player", "name": "Someone", "description": "A person.", "locationId": "hall" }
                  }
                }
                """);

            WorldPack loaded = WorldPack.Load(root, "quiet");

            if (loaded.HasScenario || loaded.Scenario.Length != 0)
            {
                Console.WriteLine("  FAIL  a pack with no scenario.md reported having one.");
                return 1;
            }

            Console.WriteLine("  ok    a pack with no scenario loads unchanged");
            return 0;
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// <b>The narrator is handed the scenario and the extractor is not.</b>
    ///
    /// The half of this design most likely to be quietly undone by a later change, and the
    /// half whose failure would be invisible: an extractor told "a child has gone missing and
    /// you were sent to investigate" would emit that premise as a <c>fact_established</c> on
    /// turn one, which reads like a model being helpful rather than like a bug.
    ///
    /// Asserted by running a real turn through <see cref="TurnEngine"/> with fakes that record
    /// what they were given, rather than by reading the code and believing it.
    /// </summary>
    private static int CheckScenarioReachesNarratorNotExtractor()
    {
        // A sentinel that cannot occur in the seed. The first version of this test searched
        // the extraction context for "marsh" and failed — because Mabb is described as an old
        // marsh-hand. A test whose sentinel collides with the world under test proves nothing
        // either way.
        const string scenario = "Three people have vanished. ZQXJV-SCENARIO-SENTINEL.";

        RecordingNarrator narrator = new();
        RecordingExtractor extractor = new();
        InMemoryWorldRepository repository = new();
        WorldState world = WorldSeeds.Marrow();

        TurnEngine engine = new(narrator, extractor, repository, historyTurns: 0, scenario: scenario);

        engine.RunTurnAsync("w", world, "*I look around.*").GetAwaiter().GetResult();

        if (narrator.SawScenario != scenario)
        {
            Console.WriteLine(
                $"  FAIL  the narrator was given scenario '{narrator.SawScenario}'.");
            return 1;
        }

        if (extractor.SawContext.Contains("ZQXJV-SCENARIO-SENTINEL", StringComparison.Ordinal))
        {
            Console.WriteLine("  FAIL  the scenario reached the extraction context.");
            return 1;
        }

        Console.WriteLine("  ok    the scenario reaches the narrator and not the extractor");
        return 0;
    }

    /// <summary>
    /// The scenario rides in the system message, above the history and above the world-state
    /// block that has to stay last.
    ///
    /// Not cosmetic: the narration-memory design puts volatile state in the final message so
    /// everything above it is a stable cacheable prefix. A scenario in the state block would
    /// break that prefix every turn to resend an identical paragraph.
    /// </summary>
    private static int CheckScenarioSitsAboveTheVolatileBlock()
    {
        const string scenario = "The pass is shut. WQNBT-VOLATILE-SENTINEL.";

        RecordingNarrator narrator = new();
        InMemoryWorldRepository repository = new();
        WorldState world = WorldSeeds.Marrow();

        TurnEngine engine = new(
            narrator, new RecordingExtractor(), repository, historyTurns: 0, scenario: scenario);

        engine.RunTurnAsync("w", world, "*I wait.*").GetAwaiter().GetResult();

        if (narrator.SawContext.Contains("WQNBT-VOLATILE-SENTINEL", StringComparison.Ordinal))
        {
            Console.WriteLine("  FAIL  the scenario was folded into the volatile world-state block.");
            return 1;
        }

        Console.WriteLine("  ok    the scenario travels separately from the world-state block");
        return 0;
    }

    /// <summary>
    /// <c>{{player}}</c> in a scenario reaches the narrator as a name, not as a token.
    ///
    /// <b>A real bug, caught by eyeballing <c>/prose</c> and not by any test.</b> The loader
    /// checks that references resolve to something; nothing was turning them into words, so the
    /// narrator was handed a literal <c>{{player}}</c> in every prompt — the token-in-the-prose
    /// failure the narration/extraction split exists to prevent, arriving through a new door.
    ///
    /// Resolved per turn rather than at load on purpose: a character renamed on turn 40 has to
    /// read correctly on turn 41.
    /// </summary>
    private static int CheckScenarioReferencesResolveToNames()
    {
        RecordingNarrator narrator = new();
        InMemoryWorldRepository repository = new();
        WorldState world = WorldSeeds.Marrow();

        string playerName = world.Player?.Name ?? "?";

        TurnEngine engine = new(
            narrator,
            new RecordingExtractor(),
            repository,
            historyTurns: 0,
            scenario: "{{player}} came to find out what is happening here.");

        engine.RunTurnAsync("w", world, "*I wait.*").GetAwaiter().GetResult();

        if (narrator.SawScenario.Contains("{{", StringComparison.Ordinal))
        {
            Console.WriteLine($"  FAIL  the narrator got an unresolved scenario: {narrator.SawScenario}");
            return 1;
        }

        if (!narrator.SawScenario.Contains(playerName, StringComparison.Ordinal))
        {
            Console.WriteLine($"  FAIL  the scenario did not resolve to the player's name.");
            return 1;
        }

        Console.WriteLine("  ok    a scenario reference resolves to a name before narration");
        return 0;
    }

    /// <summary>
    /// <b>The opening is the oldest beat the narrator remembers, and is never a turn.</b>
    ///
    /// Both halves matter. Without it in the window, the narrator answers turn one having never
    /// seen the scene the player just read, and re-establishes it from canon — contradicting
    /// details the player is looking at.
    ///
    /// And it must stay out of <c>history.jsonl</c>, because it is content rather than state.
    /// Writing it as a turn would bake today's prose into every save, so editing
    /// <c>opening.md</c> between sessions would leave the old text in old worlds — the pack/save
    /// split broken in the easiest place to break it.
    /// </summary>
    private static int CheckOpeningIsRememberedButNotRecorded()
    {
        const string opening = "The rain has not stopped. QKZRP-OPENING-SENTINEL.";

        RecordingNarrator narrator = new();
        InMemoryWorldRepository repository = new();
        WorldState world = WorldSeeds.Marrow();

        TurnEngine engine = new(
            narrator, new RecordingExtractor(), repository, historyTurns: 10, opening: opening);

        engine.RunTurnAsync("w", world, "*I look up.*").GetAwaiter().GetResult();

        if (narrator.SawBeats.Count != 1
            || narrator.SawBeats[0].Narration != opening
            || narrator.SawBeats[0].PlayerInput.Length != 0)
        {
            Console.WriteLine(
                $"  FAIL  the narrator saw {narrator.SawBeats.Count} beat(s); expected the opening, unprompted.");
            return 1;
        }

        IReadOnlyList<TurnRecord> history = repository.LoadHistoryAsync("w").GetAwaiter().GetResult();

        if (history.Count != 1)
        {
            Console.WriteLine($"  FAIL  one turn produced {history.Count} history records.");
            return 1;
        }

        if (history[0].Narration.Contains("QKZRP-OPENING-SENTINEL", StringComparison.Ordinal))
        {
            Console.WriteLine("  FAIL  the opening was written into history.");
            return 1;
        }

        Console.WriteLine("  ok    the opening enters the window as a beat and never the history");
        return 0;
    }

    /// <summary>
    /// Once the window is full of real turns, the opening falls out of it — the same lifetime
    /// any other prose has, and the whole difference between an opening and a scenario.
    /// </summary>
    private static int CheckOpeningLeavesTheWindow()
    {
        RecordingNarrator narrator = new();
        InMemoryWorldRepository repository = new();
        WorldState world = WorldSeeds.Marrow();

        // A window of two, so three turns is enough to push the opening out.
        TurnEngine engine = new(
            narrator, new RecordingExtractor(), repository, historyTurns: 2, opening: "First light.");

        for (int i = 0; i < 3; i++)
        {
            engine.RunTurnAsync("w", world, $"*Turn {i}.*").GetAwaiter().GetResult();
        }

        if (narrator.SawBeats.Any(b => b.Narration == "First light."))
        {
            Console.WriteLine("  FAIL  the opening was still in the window after the turns filled it.");
            return 1;
        }

        Console.WriteLine("  ok    the opening leaves the window once real turns fill it");
        return 0;
    }

    /// <summary>
    /// A pack with no <c>world.json</c> loads and is named after its folder. Every pack that
    /// existed before manifests is in that position.
    /// </summary>
    private static int CheckAPackWithNoManifestIsNamedAfterItsFolder()
    {
        return InTempPack("nomanifest", (root, id) =>
        {
            WorldPack pack = WorldPack.Load(root, id);

            if (pack.Manifest is not null || pack.Name != id || pack.Version.Length != 0)
            {
                Console.WriteLine($"  FAIL  a pack with no manifest reported name '{pack.Name}'.");
                return 1;
            }

            Console.WriteLine("  ok    a pack with no manifest is named after its folder");
            return 0;
        });
    }

    /// <summary>
    /// A manifest whose id disagrees with its folder is refused.
    ///
    /// The folder <i>is</i> the id, so a mismatch means a pack was copied and the directory
    /// renamed without touching the file — leaving a world that answers to two names, which is
    /// the confusion opaque permanent ids exist to prevent.
    /// </summary>
    private static int CheckAManifestMustAgreeWithItsFolder()
    {
        return InTempPack("mismatch", (root, id) =>
        {
            File.WriteAllText(
                Path.Combine(root, id, WorldPack.ManifestFile),
                """{ "id": "somebody-elses-world", "name": "Wrong" }""");

            try
            {
                WorldPack.Load(root, id);
            }
            catch (InvalidDataException)
            {
                Console.WriteLine("  ok    a manifest that disagrees with its folder is refused");
                return 0;
            }

            Console.WriteLine("  FAIL  a manifest declaring another pack's id was accepted.");
            return 1;
        });
    }

    /// <summary>
    /// A save records where it came from, once. <b>Resuming must not rewrite it</b> — the record
    /// is a fact about the past, and refreshing it on every session would quietly erase the only
    /// evidence that the pack has moved since.
    /// </summary>
    private static int CheckSaveOriginIsWrittenOnceAndNotRewritten()
    {
        string root = Path.Combine(Path.GetTempPath(), "sw-origin-" + Guid.NewGuid().ToString("N"));
        string save = Path.Combine(root, "world");

        try
        {
            SaveOrigin.WriteIfAbsent(save, "the-pack", "1.0");
            SaveOrigin.WriteIfAbsent(save, "the-pack", "2.0");

            SaveOrigin? origin = SaveOrigin.Read(save);

            if (origin?.PackVersion != "1.0")
            {
                Console.WriteLine(
                    $"  FAIL  the save origin says version '{origin?.PackVersion}'; expected the first.");
                return 1;
            }

            Console.WriteLine("  ok    a save records its origin once and resuming does not rewrite it");
            return 0;
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>Builds a minimal valid pack in a temp directory and cleans up either way.</summary>
    private static int InTempPack(string label, Func<string, string, int> check)
    {
        string root = Path.Combine(Path.GetTempPath(), $"sw-{label}-" + Guid.NewGuid().ToString("N"));
        const string id = "quiet";

        try
        {
            Directory.CreateDirectory(Path.Combine(root, id));
            File.WriteAllText(
                Path.Combine(root, id, WorldPack.SeedFile),
                """
                {
                  "turnNumber": 0,
                  "locations": { "hall": { "id": "hall", "name": "hall", "description": "A hall." } },
                  "characters": {
                    "player": { "id": "player", "name": "Someone", "description": "A person.", "locationId": "hall" }
                  }
                }
                """);

            return check(root, id);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// The repair prompt file holds two variants plus the prose explaining why each line is
    /// there. That reasoning is for a human reading the file and <b>must not reach a model</b>,
    /// so the sections are extracted by heading rather than the file being sent whole.
    /// </summary>
    private static int CheckRepairSectionsAreReadable()
    {
        PromptLibrary prompts = PromptLibrary.Load();

        string empty = PromptLibrary.Section(prompts.Repair, "empty");
        string malformed = PromptLibrary.Section(prompts.Repair, "malformed");

        if (!empty.Contains("was empty", StringComparison.OrdinalIgnoreCase)
            || !malformed.Contains("failed validation", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("  FAIL  the repair prompt sections did not read back as expected.");
            return 1;
        }

        if (empty.Contains("# Repair", StringComparison.Ordinal)
            || malformed.Contains("earned", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("  FAIL  a repair section carried the file's explanatory prose with it.");
            return 1;
        }

        Console.WriteLine("  ok    repair instructions read back by section, without the commentary");
        return 0;
    }

    /// <summary>
    /// A missing prompt file stops the engine rather than degrading it.
    ///
    /// A narrator with no prompt is not a plainer narrator; it is an unpredictable one, and the
    /// failure would surface as strange prose many turns later rather than at the moment the
    /// file went missing.
    /// </summary>
    private static int CheckAMissingPromptFileFailsLoudly()
    {
        string empty = Path.Combine(Path.GetTempPath(), "sw-noprompts-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(empty);
            PromptLibrary.Load(empty);
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("  ok    a missing prompt file fails loudly at load");
            return 0;
        }
        finally
        {
            try
            {
                Directory.Delete(empty, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        Console.WriteLine("  FAIL  a prompt directory with no files loaded anyway.");
        return 1;
    }

    /// <summary>
    /// The fingerprint tracks the prompts, or it is worse than not having one.
    ///
    /// It exists because prompts are editable files now: a score without knowing which prompt
    /// produced it is not a measurement, and unlike a <c>const</c> in a commit a file can change
    /// between two runs leaving no trace in the result. A fingerprint that did not actually
    /// follow the text would give exactly the false confidence it was added to prevent.
    /// </summary>
    private static int CheckTheFingerprintFollowsThePrompts()
    {
        string dir = Path.Combine(Path.GetTempPath(), "sw-fingerprint-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "narration.md"), "Narrate.");
            File.WriteAllText(Path.Combine(dir, "extraction.md"), "Extract.");
            File.WriteAllText(
                Path.Combine(dir, "repair.md"),
                """
                ## empty

                Repair.
                """);

            string before = PromptLibrary.Load(dir).Fingerprint;

            File.WriteAllText(Path.Combine(dir, "narration.md"), "Narrate, but differently.");
            string after = PromptLibrary.Load(dir).Fingerprint;

            if (string.Equals(before, after, StringComparison.Ordinal))
            {
                Console.WriteLine($"  FAIL  the fingerprint did not move when a prompt changed ({before}).");
                return 1;
            }

            Console.WriteLine("  ok    the prompt fingerprint follows the prompt files");
            return 0;
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// A pack shipping <c>prompts/extraction.md</c> is refused by name.
    ///
    /// <b>Refused rather than ignored.</b> A silently dropped override is worse than a refused
    /// one: the author sees a file they wrote having no effect, with nothing saying why, and
    /// concludes the feature is broken. Narration is taste and belongs to the world; extraction
    /// is correctness and is measured.
    /// </summary>
    private static int CheckAPackMayNotOverrideExtraction()
    {
        return InTempPack("extoverride", (root, id) =>
        {
            string prompts = Path.Combine(root, id, WorldPack.PromptDirectory);
            Directory.CreateDirectory(prompts);
            File.WriteAllText(Path.Combine(prompts, "extraction.md"), "Report everything, always.");

            try
            {
                WorldPack.Load(root, id);
            }
            catch (InvalidDataException e) when (e.Message.Contains("may not override extraction"))
            {
                Console.WriteLine("  ok    a pack overriding extraction is refused, by name");
                return 0;
            }

            Console.WriteLine("  FAIL  a pack was allowed to override the extraction prompt.");
            return 1;
        });
    }

    /// <summary>
    /// A pack's voice is <b>added to</b> the engine's narration prompt, never substituted for it.
    ///
    /// The engine's prompt carries correctness rules beside taste — never speak for the player,
    /// never write an internal id. Replacement would let an author drop one by omission, and it
    /// would look like a content change rather than a bug.
    /// </summary>
    private static int CheckAPackVoiceIsAddedNotSubstituted()
    {
        PromptLibrary prompts = PromptLibrary.Load();
        RecordingClient client = new();

        LlmNarrator narrator = new(client, prompts, "Write like a 1940 detective novel.");

        narrator.NarrateAsync("World state: a room.", [], "*I wait.*").GetAwaiter().GetResult();

        string system = client.SawSystem;

        if (!system.Contains("1940 detective novel", StringComparison.Ordinal))
        {
            Console.WriteLine("  FAIL  the pack's voice did not reach the narrator.");
            return 1;
        }

        if (!system.Contains("Never write an internal identifier", StringComparison.Ordinal))
        {
            Console.WriteLine("  FAIL  a pack voice replaced the engine's rules instead of adding to them.");
            return 1;
        }

        Console.WriteLine("  ok    a pack's voice is added to the engine's rules, not substituted");
        return 0;
    }

    /// <summary>Captures the system message of the last call, without spending anything.</summary>
    private sealed class RecordingClient : ILlmClient
    {
        public string SawSystem { get; private set; } = string.Empty;

        public Task<LlmResult> CompleteAsync(
            LlmCall call,
            Action<string>? onChunk = null,
            CancellationToken cancellationToken = default)
        {
            SawSystem = call.Messages.FirstOrDefault(m => m.Role == "system")?.Content ?? "";
            return Task.FromResult(LlmResult.Success("Prose.", "test", null, 0));
        }
    }

    private sealed class RecordingNarrator : INarrator
    {
        public string SawScenario { get; private set; } = string.Empty;

        public string SawContext { get; private set; } = string.Empty;

        /// <summary>The window as handed over on the most recent call.</summary>
        public IReadOnlyList<StoryBeat> SawBeats { get; private set; } = [];

        public Task<string> NarrateAsync(
            string context,
            IReadOnlyList<StoryBeat> recent,
            string playerInput,
            string scenario = "",
            CancellationToken cancellationToken = default)
        {
            SawScenario = scenario;
            SawContext = context;
            SawBeats = recent;
            return Task.FromResult("Prose.");
        }
    }

    private sealed class RecordingExtractor : IStateExtractor
    {
        public string SawContext { get; private set; } = string.Empty;

        public Task<ExtractionResult> ExtractAsync(
            string context,
            string playerInput,
            string narration,
            CancellationToken cancellationToken = default)
        {
            SawContext = context;
            return Task.FromResult(new ExtractionResult([], "{\"deltas\":[]}", null, null));
        }
    }

    private static int CheckWalkingSomewhereConnectsIt()
    {
        WorldState world = WorldSeeds.Marrow();

        DeltaApplier.Apply(
            world,
            [
                new LocationIntroduced("cellar-stair", "cellar stair", "Steps down into the dark."),
                new PlayerMoved("cellar-stair"),
            ]);

        Location? from = world.FindLocation("marrow-tavern");
        Location? to = world.FindLocation("cellar-stair");

        if (from is null || to is null || !from.Connections.Contains("cellar-stair"))
        {
            Console.WriteLine(
                "  FAIL  walking from the tavern to a new place left the tavern with no way there.");
            return 1;
        }

        Console.WriteLine("  ok    walking somewhere records the way there");
        return 0;
    }

    /// <summary>
    /// <b>And the way back.</b> The edge that matters is the reverse one.
    ///
    /// In the ashfall run the player entered <c>maintenance-shaft</c> from the vent ledge on
    /// turn 65; the forward edge would have given the shaft nothing. It is the return edge
    /// that stops a room being a hole someone falls into for seventy turns.
    ///
    /// Decided 2026-08-13 to derive both directions, knowingly against the note on
    /// <see cref="Location.Connections"/> that a one-way drop is a real thing. A one-way drop
    /// is rare; a sealed room happened 33 times. Canon is hand-editable now, so being
    /// occasionally wrong about a ledge costs one line in a JSON file.
    /// </summary>
    private static int CheckAWalkedRouteIsTwoWay()
    {
        WorldState world = WorldSeeds.Marrow();

        DeltaApplier.Apply(
            world,
            [
                new LocationIntroduced("cellar-stair", "cellar stair", "Steps down into the dark."),
                new PlayerMoved("cellar-stair"),
            ]);

        if (world.FindLocation("cellar-stair") is not { } arrived
            || !arrived.Connections.Contains("marrow-tavern"))
        {
            Console.WriteLine("  FAIL  the new place has no way back to where the player came from.");
            return 1;
        }

        Console.WriteLine("  ok    a walked route leads both ways");
        return 0;
    }

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
    /// A thing that is gone stops being in canon, and the batch stops seeing it.
    ///
    /// <see cref="ItemLost"/> is never emitted by the model — it has no schema branch, by
    /// measurement rather than oversight. The extractor rewrites an <c>item_moved</c> with no
    /// destination into it, because that is what models already produce for a thing thrown
    /// where it cannot come back from.
    ///
    /// The second half is the one worth having: an item removed mid-batch must be gone from
    /// the batch's view too, so a later delta naming it is refused rather than pointing at a
    /// ghost.
    /// </summary>
    private static int CheckALostItemLeavesCanon()
    {
        WorldState world = WorldSeeds.Marrow();

        world.Items["iron-key"] = new Item
        {
            Id = "iron-key",
            Name = "Iron key",
            Description = "A heavy iron key.",
            HolderId = Character.PlayerId,
        };

        List<StateDelta> deltas =
        [
            new ItemLost("iron-key", "thrown into the marsh"),
            new ItemStatusChanged("iron-key", "sunk"),
        ];

        ValidationOutcome outcome = DeltaValidator.Validate(world, deltas);

        if (outcome.Accepted.Count != 1 || outcome.Rejected.Count != 1)
        {
            Console.WriteLine(
                $"  FAIL  expected the loss accepted and the later change refused; got " +
                $"{outcome.Accepted.Count} accepted, {outcome.Rejected.Count} rejected.");
            return 1;
        }

        DeltaApplier.Apply(world, outcome.Accepted);

        if (world.Items.ContainsKey("iron-key"))
        {
            Console.WriteLine("  FAIL  a lost item is still in canon.");
            return 1;
        }

        Console.WriteLine("  ok    a lost item leaves canon, and the batch stops seeing it");
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
