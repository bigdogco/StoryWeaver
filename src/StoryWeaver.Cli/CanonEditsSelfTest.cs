using StoryWeaver.Core;

namespace StoryWeaver.Cli;

/// <summary>
/// Offline checks on <see cref="CanonEdits"/> — the escape hatch's actual behaviour.
///
/// The cases that matter are the cascades. Removing a character is not one operation, it is a
/// decision about everything that referred to them, and the console and a future window must
/// make that decision identically — which is why it lives in Core and is tested here rather
/// than being whatever each client's loop happens to do.
///
/// The other half is what is *not* repaired. An item lying in a removed place has no right
/// answer, and inventing one would move somebody's belongings somewhere they never were. Those
/// cases assert that the mess is left **and reported**, which is the honest behaviour rather
/// than the tidy one.
/// </summary>
internal static class CanonEditsSelfTest
{
    public static int Run()
    {
        Console.WriteLine("CanonEdits self-test");

        int failures = 0;

        failures += CheckDescribeReachesAllThreeKinds();
        failures += CheckRewordingLeavesKnowledgeAlone();
        failures += CheckForgetIsOneCharacterOnly();
        failures += CheckRemovingSomeoneDropsWhatTheyHeld();
        failures += CheckRemovingSomeoneOffstageLeavesAMessAndSaysSo();
        failures += CheckRemovingAPlaceStrandsAndDisconnects();
        failures += CheckRemovingAFactIsForgottenByEveryone();
        failures += CheckConsequencesMatchWhatHappens();

        Console.WriteLine(failures == 0
            ? "  all CanonEdits checks passed"
            : $"  {failures} CanonEdits check(s) failed");

        return failures == 0 ? 0 : 1;
    }

    private static int CheckDescribeReachesAllThreeKinds()
    {
        WorldState world = Sample();

        CanonEdits.Describe("innkeeper-hald", "Broader than the doorway.")(world);
        CanonEdits.Describe("marrow-tavern", "Low, and full of smoke.")(world);
        CanonEdits.Describe("iron-key", "Older than the lock it opens.")(world);

        if (world.Characters["innkeeper-hald"].Description != "Broader than the doorway."
            || world.Locations["marrow-tavern"].Description != "Low, and full of smoke."
            || world.Items["iron-key"].Description != "Older than the lock it opens.")
        {
            Console.WriteLine("  FAIL  describe did not reach all three kinds.");
            return 1;
        }

        Console.WriteLine("  ok    a description can be rewritten on a person, a place or a thing");
        return 0;
    }

    /// <summary>
    /// Knowledge holds fact ids, never text — so rewording is invisible to everyone who knows
    /// it, and nobody ends up knowing a different version. Asserted because it is the property
    /// the console promises out loud when it offers this.
    /// </summary>
    private static int CheckRewordingLeavesKnowledgeAlone()
    {
        WorldState world = Sample();

        CanonEdits.RewordFact("well-boarded", "The well has been boarded over since winter.")(world);

        if (world.Facts["well-boarded"].Text != "The well has been boarded over since winter.")
        {
            Console.WriteLine("  FAIL  the fact was not reworded.");
            return 1;
        }

        if (!world.Characters["innkeeper-hald"].Knows.Contains("well-boarded")
            || !world.Characters["player"].Knows.Contains("well-boarded"))
        {
            Console.WriteLine("  FAIL  rewording changed who knows it.");
            return 1;
        }

        Console.WriteLine("  ok    rewording a fact leaves who knows it untouched");
        return 0;
    }

    private static int CheckForgetIsOneCharacterOnly()
    {
        WorldState world = Sample();

        CanonEdits.Forget("innkeeper-hald", "well-boarded")(world);

        if (world.Characters["innkeeper-hald"].Knows.Contains("well-boarded"))
        {
            Console.WriteLine("  FAIL  they did not forget it.");
            return 1;
        }

        if (!world.Characters["player"].Knows.Contains("well-boarded"))
        {
            Console.WriteLine("  FAIL  somebody else forgot it too.");
            return 1;
        }

        Console.WriteLine("  ok    forgetting reaches one character and nobody else");
        return 0;
    }

    private static int CheckRemovingSomeoneDropsWhatTheyHeld()
    {
        WorldState world = Sample();
        world.Items["iron-key"].HolderId = "innkeeper-hald";
        world.Items["iron-key"].LocationId = null;

        CanonEdits.Remove("innkeeper-hald")(world);

        Item key = world.Items["iron-key"];

        if (world.Characters.ContainsKey("innkeeper-hald"))
        {
            Console.WriteLine("  FAIL  the character was not removed.");
            return 1;
        }

        if (key.HolderId is not null || key.LocationId != "marrow-tavern")
        {
            Console.WriteLine($"  FAIL  the key ended up holder='{key.HolderId}' location='{key.LocationId}'.");
            return 1;
        }

        if (CanonRefresh.Check(world).Count != 0)
        {
            Console.WriteLine("  FAIL  removing someone who was somewhere left canon inconsistent.");
            return 1;
        }

        Console.WriteLine("  ok    removing someone leaves what they held where they stood");
        return 0;
    }

    /// <summary>
    /// The honest half. Somebody offstage is nowhere, so what they were carrying has nowhere to
    /// be put — and putting it somewhere they never were would be worse than leaving it.
    /// </summary>
    private static int CheckRemovingSomeoneOffstageLeavesAMessAndSaysSo()
    {
        WorldState world = Sample();
        world.Characters["drinker-mabb"].LocationId = null;
        world.Items["iron-key"].HolderId = "drinker-mabb";
        world.Items["iron-key"].LocationId = null;

        IReadOnlyList<string> consequences = CanonEdits.ConsequencesOfRemoving(world, "drinker-mabb");

        if (!consequences.Any(c => c.Contains("held by nobody", StringComparison.Ordinal)))
        {
            Console.WriteLine($"  FAIL  the warning did not say the key would be orphaned: [{string.Join("; ", consequences)}].");
            return 1;
        }

        CanonEdits.Remove("drinker-mabb")(world);

        if (!CanonRefresh.Check(world).Any(w => w.Contains("iron-key", StringComparison.Ordinal)))
        {
            Console.WriteLine("  FAIL  the orphaned item was not reported afterwards.");
            return 1;
        }

        Console.WriteLine("  ok    an offstage removal orphans, warns first, and reports after");
        return 0;
    }

    private static int CheckRemovingAPlaceStrandsAndDisconnects()
    {
        WorldState world = Sample();
        world.Locations["marrow-square"].Connections.Add("marrow-tavern");
        world.Locations["marrow-tavern"].Connections.Add("marrow-square");

        CanonEdits.Remove("marrow-tavern")(world);

        if (world.Characters["innkeeper-hald"].LocationId is not null
            || world.Characters["player"].LocationId is not null)
        {
            Console.WriteLine("  FAIL  people in the removed place are not offstage.");
            return 1;
        }

        if (world.Locations["marrow-square"].Connections.Contains("marrow-tavern"))
        {
            Console.WriteLine("  FAIL  a connection to the removed place survived.");
            return 1;
        }

        Console.WriteLine("  ok    removing a place strands people offstage and drops connections");
        return 0;
    }

    private static int CheckRemovingAFactIsForgottenByEveryone()
    {
        WorldState world = Sample();

        CanonEdits.Remove("well-boarded")(world);

        if (world.Facts.ContainsKey("well-boarded")
            || world.Characters.Values.Any(c => c.Knows.Contains("well-boarded")))
        {
            Console.WriteLine("  FAIL  a removed fact is still known by somebody.");
            return 1;
        }

        if (CanonRefresh.Check(world).Count != 0)
        {
            Console.WriteLine("  FAIL  removing a fact left dangling knowledge.");
            return 1;
        }

        Console.WriteLine("  ok    removing a fact is forgotten by everyone who knew it");
        return 0;
    }

    /// <summary>
    /// The warning has to be true, or it is worse than no warning. This is the check that keeps
    /// the two in step: whatever the consequences claim will happen is what removal then does.
    /// </summary>
    private static int CheckConsequencesMatchWhatHappens()
    {
        WorldState world = Sample();
        world.Locations["marrow-square"].Connections.Add("marrow-tavern");

        IReadOnlyList<string> consequences = CanonEdits.ConsequencesOfRemoving(world, "marrow-tavern");

        bool saidStranded = consequences.Any(c => c.Contains("innkeeper-hald", StringComparison.Ordinal));
        bool saidConnections = consequences.Any(c => c.Contains("connect", StringComparison.Ordinal));
        bool saidItem = consequences.Any(c => c.Contains("iron-key", StringComparison.Ordinal));

        if (!saidStranded || !saidConnections || !saidItem)
        {
            Console.WriteLine(
                $"  FAIL  consequences missed something: stranded={saidStranded}, " +
                $"connections={saidConnections}, item={saidItem}.");
            return 1;
        }

        CanonEdits.Remove("marrow-tavern")(world);

        if (world.Characters["innkeeper-hald"].LocationId is not null
            || world.Locations["marrow-square"].Connections.Contains("marrow-tavern")
            || world.Items["iron-key"].LocationId != "marrow-tavern")
        {
            Console.WriteLine("  FAIL  removal did not do what the consequences promised.");
            return 1;
        }

        // And the item it warned about is exactly what Check now complains of.
        if (!CanonRefresh.Check(world).Any(w => w.Contains("iron-key", StringComparison.Ordinal)))
        {
            Console.WriteLine("  FAIL  the item the warning named is not reported afterwards.");
            return 1;
        }

        Console.WriteLine("  ok    the warning says exactly what the removal then does");
        return 0;
    }

    /// <summary>A small world that warns about nothing, so any warning is the test's own doing.</summary>
    private static WorldState Sample()
    {
        WorldState world = new();

        world.Locations["marrow-tavern"] = new Location { Id = "marrow-tavern", Name = "The Drowned Crow" };
        world.Locations["marrow-square"] = new Location { Id = "marrow-square", Name = "The square" };
        world.Facts["well-boarded"] = new Fact { Id = "well-boarded", Text = "The well is boarded over." };

        world.Characters["player"] = new Character { Id = "player", Name = "Pavel", LocationId = "marrow-tavern" };
        world.Characters["innkeeper-hald"] = new Character { Id = "innkeeper-hald", Name = "Hald", LocationId = "marrow-tavern" };
        world.Characters["drinker-mabb"] = new Character { Id = "drinker-mabb", Name = "Mabb", LocationId = "marrow-square" };

        world.Characters["player"].Knows.Add("well-boarded");
        world.Characters["innkeeper-hald"].Knows.Add("well-boarded");

        world.Items["iron-key"] = new Item
        {
            Id = "iron-key",
            Name = "An iron key",
            LocationId = "marrow-tavern",
        };

        return world;
    }
}
