using StoryWeaver.Core;

namespace StoryWeaver.Harness;

/// <summary>
/// Offline checks on <see cref="CanonRefresh"/> — Update State, on both surfaces.
///
/// The feature is a safety net for hand-edited canon, so the cases that matter are the ones a
/// person actually produces with a text editor: an entity renamed by its key but not its field,
/// a location id that no longer exists, an item put down while still being carried. And, just
/// as importantly, the shapes that are *legal* and must not be warned about — an offstage
/// character, and knowledge of a lore entry rather than a fact.
/// </summary>
internal static class CanonRefreshSelfTest
{
    public static int Run()
    {
        Console.WriteLine("Update State self-test");

        int failures = 0;

        failures += CheckAnExternalEditIsSeen();
        failures += CheckAdditionsAndRemovals();
        failures += CheckNoChangeIsReportedAsNoChange();
        failures += CheckSetOrderIsNotAChange();
        failures += CheckOffstageIsLegalAndDanglingIsNot();
        failures += CheckLoreKnowledgeIsLegal();
        failures += CheckItemPlacement();
        failures += CheckKeyMustMatchId();
        failures += CheckMalformedIds();
        failures += CheckNothingOnDisk();

        Console.WriteLine(failures == 0
            ? "  all Update State checks passed"
            : $"  {failures} Update State check(s) failed");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>The whole point: an edit made outside the session is picked up and named.</summary>
    private static int CheckAnExternalEditIsSeen()
    {
        WorldState session = Sample();
        WorldState onDisk = Sample();
        onDisk.Characters["innkeeper-hald"].Mood = "furious";

        RefreshReport report = Read(session, onDisk);

        if (report.Changes.Count != 1 || report.Changes[0] != "changed character innkeeper-hald")
        {
            Console.WriteLine($"  FAIL  external edit: got [{string.Join("; ", report.Changes)}].");
            return 1;
        }

        if (report.World?.Characters["innkeeper-hald"].Mood != "furious")
        {
            Console.WriteLine("  FAIL  external edit: the returned world does not carry it.");
            return 1;
        }

        Console.WriteLine("  ok    an external edit is seen, named, and carried back");
        return 0;
    }

    private static int CheckAdditionsAndRemovals()
    {
        WorldState session = Sample();
        WorldState onDisk = Sample();
        onDisk.Locations["the-sunken-chapel"] = new Location { Id = "the-sunken-chapel", Name = "Chapel" };
        onDisk.Characters.Remove("drinker-mabb");
        onDisk.TurnNumber = 12;

        RefreshReport report = Read(session, onDisk);

        string[] expected =
        [
            "turn 4 → 12",
            "removed character drinker-mabb",
            "added place the-sunken-chapel",
        ];

        foreach (string line in expected)
        {
            if (!report.Changes.Contains(line))
            {
                Console.WriteLine($"  FAIL  expected '{line}' in [{string.Join("; ", report.Changes)}].");
                return 1;
            }
        }

        Console.WriteLine("  ok    additions, removals and the turn counter are reported");
        return 0;
    }

    private static int CheckNoChangeIsReportedAsNoChange()
    {
        RefreshReport report = Read(Sample(), Sample());

        if (!report.Unchanged || report.Changes.Count != 0)
        {
            Console.WriteLine($"  FAIL  identical worlds reported [{string.Join("; ", report.Changes)}].");
            return 1;
        }

        Console.WriteLine("  ok    an unedited file reports nothing to update");
        return 0;
    }

    /// <summary>
    /// <c>Knows</c> and <c>Connections</c> are sets. If their enumeration order leaked into the
    /// comparison, every reload would report every character as changed — which is the kind of
    /// noise that makes a person stop reading the output.
    /// </summary>
    private static int CheckSetOrderIsNotAChange()
    {
        WorldState session = Sample();
        WorldState onDisk = Sample();

        session.Characters["innkeeper-hald"].Knows.Clear();
        foreach (string id in new[] { "well-boarded", "cult-of-the-blind" })
        {
            session.Characters["innkeeper-hald"].Knows.Add(id);
        }

        onDisk.Characters["innkeeper-hald"].Knows.Clear();
        foreach (string id in new[] { "cult-of-the-blind", "well-boarded" })
        {
            onDisk.Characters["innkeeper-hald"].Knows.Add(id);
        }

        RefreshReport report = Read(session, onDisk);

        if (report.Changes.Count != 0)
        {
            Console.WriteLine($"  FAIL  set ordering reported as a change: [{string.Join("; ", report.Changes)}].");
            return 1;
        }

        Console.WriteLine("  ok    set ordering is not mistaken for an edit");
        return 0;
    }

    /// <summary>
    /// The invariant `PROJECT.md` had stated wrongly. A null location is offstage and correct;
    /// only a location naming nothing is a problem.
    /// </summary>
    private static int CheckOffstageIsLegalAndDanglingIsNot()
    {
        WorldState world = Sample();
        world.Characters["warrior-mike"] = new Character { Id = "warrior-mike", Name = "Mike", LocationId = null };

        if (Warns(world, "warrior-mike"))
        {
            Console.WriteLine("  FAIL  an offstage character was warned about.");
            return 1;
        }

        world.Characters["inspector-mona"].LocationId = "nowhere-at-all";

        if (!Warns(world, "nowhere-at-all"))
        {
            Console.WriteLine("  FAIL  a character in a non-existent place was not warned about.");
            return 1;
        }

        Console.WriteLine("  ok    offstage is legal; a dangling location is not");
        return 0;
    }

    /// <summary>
    /// Facts and lore share an id namespace. Checking `world.Facts` alone would warn about
    /// every lore entry anyone has ever heard of — noise on a correct world.
    /// </summary>
    private static int CheckLoreKnowledgeIsLegal()
    {
        WorldState world = Sample();
        world.Characters["innkeeper-hald"].Knows.Add("cult-of-the-blind");

        LoreBook lore = new([new LoreEntry
        {
            Id = "cult-of-the-blind",
            Title = "The Cult of the Blind",
            Body = "They take the eyes first.",
        }]);

        if (CanonRefresh.Check(world, lore).Any(w => w.Contains("cult-of-the-blind", StringComparison.Ordinal)))
        {
            Console.WriteLine("  FAIL  knowing a lore entry was warned about.");
            return 1;
        }

        if (!CanonRefresh.Check(world, LoreBook.Empty)
                .Any(w => w.Contains("cult-of-the-blind", StringComparison.Ordinal)))
        {
            Console.WriteLine("  FAIL  knowing something that is neither fact nor lore was not warned about.");
            return 1;
        }

        Console.WriteLine("  ok    known lore is legal; knowing nothing-at-all is not");
        return 0;
    }

    private static int CheckItemPlacement()
    {
        WorldState world = Sample();
        world.Items["nowhere-knife"] = new Item { Id = "nowhere-knife", Name = "A knife" };
        world.Items["both-knife"] = new Item
        {
            Id = "both-knife",
            Name = "Another knife",
            LocationId = "marrow-tavern",
            HolderId = "innkeeper-hald",
        };

        if (!Warns(world, "neither held nor anywhere") || !Warns(world, "is both held by"))
        {
            Console.WriteLine("  FAIL  item placement: expected warnings for 'neither' and 'both'.");
            return 1;
        }

        Console.WriteLine("  ok    an item that is nowhere, or in two places, is reported");
        return 0;
    }

    /// <summary>
    /// The hand-edit failure. Rename the key in `canon.json`, miss the `id` field inside, and
    /// the entity looks perfectly correct while being unreachable by its own id.
    /// </summary>
    private static int CheckKeyMustMatchId()
    {
        WorldState world = Sample();
        world.Characters["mabb-the-elder"] = new Character { Id = "drinker-mabb", Name = "Mabb" };

        if (!Warns(world, "cannot be found by its own id"))
        {
            Console.WriteLine("  FAIL  a key disagreeing with its id was not reported.");
            return 1;
        }

        Console.WriteLine("  ok    a key that disagrees with its own id is reported");
        return 0;
    }

    /// <summary>
    /// The check that only became possible when `EntityId` moved into Core. Ids are matched by
    /// exact string comparison everywhere, so `Warrior_Mike` is a different thing from
    /// `warrior-mike` to all of it and the same thing to a reader.
    ///
    /// The negative half matters as much: every id a real save contains must stay silent, or
    /// this becomes noise on correct worlds. Measured at 549 ids across 11 saves, zero
    /// malformed, before the warning was written.
    /// </summary>
    private static int CheckMalformedIds()
    {
        if (CanonRefresh.Check(Sample()).Any(w => w.Contains("not a usable id", StringComparison.Ordinal)))
        {
            Console.WriteLine("  FAIL  a correct world produced an id warning.");
            return 1;
        }

        foreach (string bad in new[] { "Warrior_Mike", "warrior--mike", "-mike", "Warrior Mike" })
        {
            WorldState world = Sample();
            world.Characters[bad] = new Character { Id = bad, Name = "Mike", LocationId = "marrow-tavern" };

            if (!Warns(world, "not a usable id"))
            {
                Console.WriteLine($"  FAIL  '{bad}' was not reported as a malformed id.");
                return 1;
            }
        }

        Console.WriteLine("  ok    malformed ids are reported; a correct world stays silent");
        return 0;
    }

    private static int CheckNothingOnDisk()
    {
        RefreshReport report = CanonRefresh
            .ReadAsync("never-saved", Sample(), new InMemoryWorldRepository())
            .GetAwaiter().GetResult();

        if (!report.NothingOnDisk || report.World is not null)
        {
            Console.WriteLine("  FAIL  an unsaved world should report nothing on disk.");
            return 1;
        }

        Console.WriteLine("  ok    a session that has never saved says so");
        return 0;
    }

    private static bool Warns(WorldState world, string fragment) =>
        CanonRefresh.Check(world).Any(w => w.Contains(fragment, StringComparison.Ordinal));

    private static RefreshReport Read(WorldState session, WorldState onDisk)
    {
        InMemoryWorldRepository repository = new();
        repository.SaveAsync("test", onDisk).GetAwaiter().GetResult();

        return CanonRefresh.ReadAsync("test", session, repository).GetAwaiter().GetResult();
    }

    /// <summary>A small correct world. Every check starts from something that warns about nothing.</summary>
    private static WorldState Sample()
    {
        WorldState world = new() { TurnNumber = 4 };

        world.Locations["marrow-tavern"] = new Location { Id = "marrow-tavern", Name = "The Drowned Crow" };
        world.Locations["marrow-square"] = new Location { Id = "marrow-square", Name = "The square" };
        world.Facts["well-boarded"] = new Fact { Id = "well-boarded", Text = "The well is boarded over." };

        world.Characters["player"] = new Character { Id = "player", Name = "Pavel", LocationId = "marrow-tavern" };
        world.Characters["innkeeper-hald"] = new Character { Id = "innkeeper-hald", Name = "Hald", LocationId = "marrow-tavern" };
        world.Characters["drinker-mabb"] = new Character { Id = "drinker-mabb", Name = "Mabb", LocationId = "marrow-tavern" };
        world.Characters["inspector-mona"] = new Character { Id = "inspector-mona", Name = "Mona", LocationId = "marrow-square" };

        world.Characters["innkeeper-hald"].Knows.Add("well-boarded");

        return world;
    }
}
