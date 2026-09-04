using StoryWeaver.Core;

namespace StoryWeaver.Harness;

/// <summary>
/// The hardcoded starting world, shared by the extraction eval and the self-tests.
///
/// Shared deliberately: an eval that scores against a different world than the one being
/// played is measuring the wrong thing, and two copies of a seed drift the moment one is
/// edited.
/// </summary>
public static class WorldSeeds
{
    /// <summary>
    /// Two locations, two NPCs, and one fact that exactly one NPC knows.
    ///
    /// The single-holder fact is the important part: a scene with no secrets cannot show
    /// whether per-character knowledge is working, which is the whole premise.
    /// </summary>
    public static WorldState Marrow()
    {
        WorldState world = new();

        world.Locations["marrow-tavern"] = new Location
        {
            Id = "marrow-tavern",
            Name = "The Drowned Crow",
            Description =
                "A low-ceilinged taproom in the town of Marrow. Peat smoke, spilled ale, and " +
                "the sour cold that comes off the marsh outside. A handful of locals keep to " +
                "their own tables and their own business.",
            Connections = { "marrow-square" },
        };

        world.Locations["marrow-square"] = new Location
        {
            Id = "marrow-square",
            Name = "Marrow Square",
            Description = "A rutted market square, mostly empty. The well at its centre is boarded over.",
            Connections = { "marrow-tavern" },
        };

        world.Characters[Character.PlayerId] = new Character
        {
            Id = Character.PlayerId,
            Name = "You",
            Description = "A traveller, recently arrived in Marrow.",
            LocationId = "marrow-tavern",
        };

        world.Characters["innkeeper-hald"] = new Character
        {
            Id = "innkeeper-hald",
            Name = "Hald",
            Description =
                "The innkeeper of the Drowned Crow. Heavyset, watchful, wipes the same patch " +
                "of counter when he is thinking.",
            LocationId = "marrow-tavern",
            Status = "normal",
            Mood = "guarded",
            RelationshipToPlayer = new Relationship(-10, "suspicious of strangers"),
            Knows = { "well-boarded" },
        };

        world.Characters["drinker-mabb"] = new Character
        {
            Id = "drinker-mabb",
            Name = "Mabb",
            Description = "An old marsh-hand nursing a mug in the corner. Talks when drunk, which is often.",
            LocationId = "marrow-tavern",
            Status = "drunk",
            Mood = "maudlin",
        };

        world.Facts["well-boarded"] = new Fact
        {
            Id = "well-boarded",
            Text = "The well in Marrow Square was boarded over after something was found in it.",
            EstablishedTurn = 0,
        };

        return world;
    }

    /// <summary>
    /// The lore the eval scenarios run against.
    ///
    /// Hand-built rather than read from <c>worlds/marrow/lore/</c> deliberately: an eval that
    /// changes score because somebody edited a pack file is measuring the wrong thing. The
    /// text is deliberately close to the shipped entries so the measurement stays
    /// representative.
    /// </summary>
    public static LoreBook MarrowLore() => new(
    [
        new LoreEntry
        {
            Id = "kingdom-of-vaska",
            Title = "The Kingdom of Vaska",
            Body =
                "The kingdom these marshes belong to, ruled from the capital at Astaria. King " +
                "Aldric the Fourth has held the throne for nineteen years — long enough that " +
                "most people in the marsh towns have never known another king, and far enough " +
                "away that it makes very little difference to them which one it is.",
            Keys = ["kingdom", "vaska", "king"],
            Always = true,
            Common = true,
            Priority = 30,
        },
        new LoreEntry
        {
            Id = "kings-investigators",
            Title = "The King's Investigators",
            Body =
                "An order answering directly to the crown, empowered to enter any holding and " +
                "question any subject without a local warrant. They wear no uniform; an " +
                "investigator carries a seal, and the seal is the authority. In the marsh towns " +
                "they are half-rumour — everyone has heard of them, almost nobody has met one.",
            Keys = ["investigator", "king's men", "the order"],
            Priority = 10,
        },
        new LoreEntry
        {
            Id = "cult-of-the-blind",
            Title = "The Cult of the Blind",
            Body =
                "An old faith worshipping Shurus, the Drowned Father — a god of still water and " +
                "things kept under it. Its sign is a weeping woman with her eyes gouged out. The " +
                "cult holds that the marsh preserves what it takes: a follower the water claims " +
                "wakes in the deep bog and becomes the marsh itself, forever.",
            Keys = ["cult", "shurus", "drowned father"],
            Priority = 20,
        },
    ]);

    /// <summary>
    /// Marrow where Hald has heard of the cult and nobody else has.
    ///
    /// The asymmetry is the entire point, and it mirrors the seeded fact `well-boarded` that
    /// exactly one NPC knows — the shape that proved per-character knowledge works.
    /// </summary>
    public static WorldState Marrow_WithLore()
    {
        WorldState world = Marrow();
        world.Characters["innkeeper-hald"].Knows.Add("cult-of-the-blind");
        return world;
    }

    /// <summary>
    /// Marrow with a plan already in canon naming one of two objects.
    ///
    /// Written after <c>wrong-object-acted-on</c> failed to reproduce a real play failure. In
    /// isolation the model records nothing about two objects and correctly declines to guess.
    /// The false canon in play needed *accumulated context*: by turn 48 the world already held
    /// "the weeping woman capstone must be ground to powder and packed into the mortar joints",
    /// and when grinding actually happened the action was matched to that plan — which named
    /// the wrong stone.
    ///
    /// A scenario without the canon that caused the failure cannot reproduce the failure. The
    /// same lesson as the implicit-lore case, from a different direction.
    /// </summary>
    public static WorldState Marrow_WithGrindingPlan()
    {
        WorldState world = Marrow();

        world.Facts["capstone-powder-mortar-salt"] = new Fact
        {
            Id = "capstone-powder-mortar-salt",
            Text =
                "The weeping woman capstone must be ground to powder and packed into the mortar " +
                "joints around the base of the well with coarse salt.",
            EstablishedTurn = 0,
        };

        world.Characters[Character.PlayerId].Knows.Add("capstone-powder-mortar-salt");

        // Both objects already exist, which is the state play was in: two things the player
        // had been carrying for turns, one of which the plan names. Without them the model
        // emits status changes against ids that do not exist and the validator rejects
        // everything, which measures the validator rather than the model.
        world.Items["weeping-woman-capstone"] = new Item
        {
            Id = "weeping-woman-capstone",
            Name = "The weeping woman capstone",
            Description =
                "A dark, slick carved stone depicting a weeping woman with gouged-out eyes. " +
                "Water beads and runs from it constantly.",
            LocationId = "marrow-tavern",
        };

        world.Items["pale-chunks"] = new Item
        {
            Id = "pale-chunks",
            Name = "Morwenna's pale chunks",
            Description =
                "Three dry, pale chunks crusted with something white, smelling thinly of old " +
                "copper. They came out of an oilcloth bundle.",
            LocationId = "marrow-tavern",
        };

        return world;
    }

    /// <summary>
    /// Marrow late, standing at a well the story has been circling for forty turns.
    ///
    /// Written after <c>place-changing</c> failed to reproduce twice. Three things were wrong
    /// with running that narration against the base seed, and the first alone could account for
    /// the null result:
    ///
    /// <list type="number">
    /// <item><b>The player was in the tavern.</b> The narration is a well in the square being
    /// worked on, scored against a world where the player is somewhere else entirely. A place
    /// the player is not standing in is background, and background is exactly what extraction
    /// is right to ignore — the same rule <c>atmosphere</c> and <c>narrator-mention</c>
    /// establish. The scenario was measuring the mention rule, not the misfiling.</item>
    /// <item><b>The icon and the wire did not exist.</b> The player acts on two objects that
    /// were not in canon. Same lesson as <see cref="Marrow_WithGrindingPlan"/>: without them
    /// the model has nothing to attach a change to, and a scenario that cannot express the
    /// right answer cannot show a wrong one.</item>
    /// <item><b>The well carried no weight.</b> In play it had forty turns and a dozen facts
    /// behind it. A boarded well nobody has mentioned since turn 0 is scenery.</item>
    /// </list>
    ///
    /// <b>What is deliberately not seeded: prior facts of the shape under test.</b> The real
    /// session had six accumulating, and by the fourth the model was arguably following canon's
    /// own precedent. Seeding that precedent would reproduce the failure by supplying it, which
    /// proves nothing about whether the schema is the cause. If this seed reproduces without
    /// it, the finding is clean. If it does not, adding the precedent is the next experiment
    /// and a different, weaker claim — worth running, worth labelling as what it is.
    ///
    /// The seeded facts avoid the words the scoring rule matches on, so nothing here can be
    /// mistaken for the thing being detected.
    ///
    /// <b>A first draft seeded the causal mechanism itself</b> — "tarnished metal touched to
    /// the well's cap provokes an answer from the shaft" — which is the discovery the narration
    /// makes. It measurably suppressed the failure (5/7 against 7/7 on the base seed), because
    /// the model had been told the answer and mostly restated it. A fixture must supply the
    /// weight behind a scene without supplying the scene's content.
    /// </summary>
    public static WorldState Marrow_WellSignificant()
    {
        WorldState world = Marrow_Late();

        // Standing at it, not hearing about it from the taproom.
        world.Characters[Character.PlayerId].LocationId = "marrow-square";
        world.Characters["guard-tomas"].LocationId = "drowned-lane";

        world.Locations["marrow-square"].Description =
            "A rutted market square, empty at this hour. The well at its centre is capped with " +
            "planking spiked down at every edge, and the stones around it are dark and wet.";

        // The two objects the narration acts on. Held rather than placed: they have been
        // carried for turns, which is the state play was in.
        world.Items["silver-icon"] = new Item
        {
            Id = "silver-icon",
            Name = "Tarnished silver icon",
            Description =
                "A weeping woman worked in silver, black with tarnish and unnaturally cold. It " +
                "came up out of the shaft on a rope.",
            HolderId = Character.PlayerId,
        };

        world.Items["bronze-wire"] = new Item
        {
            Id = "bronze-wire",
            Name = "Length of bronze wire",
            Description =
                "A broken arm's length of green-crusted bronze, cut away from the altar in the " +
                "sunken watchtower.",
            HolderId = Character.PlayerId,
        };

        // Weight. Why this well, why tonight, and why touching metal to it means anything —
        // the accumulated significance the base seed has no way to express.
        (string Id, string Text)[] history =
        [
            ("well-spiked-shut",
                "The well in Marrow Square was capped with planking and spiked shut after the " +
                "thing in it was brought up."),
            ("icon-from-shaft",
                "The tarnished silver icon was brought up out of the well shaft on a rope."),
            ("wire-from-altar",
                "The length of bronze wire was cut from the cult's altar in the sunken " +
                "watchtower."),
            ("seepage-worsening",
                "The black seepage from the well's cracks has come heavier every night this week."),
            ("close-before-moon-turns",
                "Morwenna said the well must be closed before the moon turns, or the marsh takes " +
                "the village."),
            ("three-nights-at-the-well",
                "The player has worked at the well for three nights running without closing it."),
        ];

        foreach ((string id, string text) in history)
        {
            world.Facts[id] = new Fact { Id = id, Text = text, EstablishedTurn = 34 };
            world.Characters[Character.PlayerId].Knows.Add(id);
        }

        world.TurnNumber = 42;

        return world;
    }

    /// <summary>
    /// Marrow with a shape under a tarp — an item that is about to turn out to be a person.
    ///
    /// Taken from the 51-turn session at turn 12, where the extractor introduced
    /// <c>tarp-covered-shape</c> as an item, correctly: a covered shape is an object until it
    /// moves. It then spent four turns trying to treat it as a character and being refused
    /// every time, and one of those refusals took a real revelation out of canon.
    ///
    /// Seeded as an item rather than narrated into existence, because the failure is not in the
    /// introduction — that part was right. It is in what happens next.
    /// </summary>
    public static WorldState Marrow_WithCoveredShape()
    {
        WorldState world = Marrow();

        world.Items["tarp-covered-shape"] = new Item
        {
            Id = "tarp-covered-shape",
            Name = "Shape under a tarp",
            Description =
                "A shape covered by a heavy, salt-stained tarp, lying on a bed of rotting " +
                "reeds against the far wall.",
            LocationId = "marrow-tavern",
        };

        return world;
    }

    /// <summary>
    /// Marrow where the player already carries a medallion, and a second identical one is
    /// hanging in the room.
    ///
    /// From turn 40 of the 51-turn session. The player had taken one medallion off a body
    /// twenty turns earlier; lifting a second off a shrine, extraction emitted
    /// <c>item_renamed</c> on the <i>first</i> one, with a description ending "An exact match
    /// for…". It noticed they were identical and collapsed them into a single object.
    ///
    /// <b>The distance is deliberate.</b> The first medallion is seeded as already held and
    /// long familiar, because that is what makes the merge tempting — the model is not
    /// confusing two things in front of it, it is matching a new object against a remembered
    /// one. A scenario with both objects in the same paragraph would be the
    /// <c>two-objects</c> case, which already scores clean.
    /// </summary>
    public static WorldState Marrow_WithMedallionAlready()
    {
        WorldState world = Marrow();

        world.Items["weeping-woman-medallion"] = new Item
        {
            Id = "weeping-woman-medallion",
            Name = "Tarnished silver medallion of the weeping woman",
            Description =
                "A heavy silver disk depicting a weeping woman with bowed head and gouged, " +
                "empty eye sockets. Ice-cold to the touch.",
            HolderId = Character.PlayerId,
        };

        world.Items["crude-wooden-shrine"] = new Item
        {
            Id = "crude-wooden-shrine",
            Name = "Crude wooden shrine",
            Description =
                "A shrine carved into the shape of a kneeling woman with a blank, eyeless " +
                "face. A tarnished silver medallion hangs at its neck on a rotting hemp cord.",
            LocationId = "marrow-tavern",
        };

        world.Characters[Character.PlayerId].Knows.Add("well-boarded");

        return world;
    }

    /// <summary>
    /// A cellar with a sluice gate in it, and a coil of chain on the floor.
    ///
    /// Rebuilt from turn 44 of the `ashfall` session after two easier versions failed to
    /// reproduce anything. Throwing a cup across a room scored 10/12 and 12/12 — the model
    /// handles an object that simply lands somewhere.
    ///
    /// What the three real failures had in common was **a fixture**: a cup on "the floor", a
    /// cable "in the shaft", a chain "around the gate". None of those is a location you can
    /// move an item to, so the model wrote the destination into the status field, where it
    /// fits and where nothing checks it. The gate is the whole point of this seed.
    ///
    /// The chain starts on the floor rather than in hand, because the real turn had the player
    /// pick it up and attach it in one move — and it is the ending, not the picking up, that
    /// goes wrong.
    /// </summary>
    public static WorldState Marrow_WithChainAndGate()
    {
        WorldState world = Marrow();

        world.Locations["flood-cellar"] = new Location
        {
            Id = "flood-cellar",
            Name = "The flood cellar",
            Description =
                "A brick undercroft below the tavern, ankle-deep in cold water. A rusted " +
                "sluice gate closes off the channel at the far end, its brackets weeping " +
                "orange down the stone.",
            Connections = { "marrow-tavern" },
        };

        world.Locations["marrow-tavern"].Connections.Add("flood-cellar");
        world.Characters[Character.PlayerId].LocationId = "flood-cellar";

        world.Items["iron-chain"] = new Item
        {
            Id = "iron-chain",
            Name = "Length of iron chain",
            Description = "A cold, pitted length of iron chain, coiled on the wet brick.",
            LocationId = "flood-cellar",
        };

        return world;
    }

    /// <summary>
    /// The player, in the square, holding something they are about to throw away for good.
    ///
    /// The marsh is the point: it is named in the square's description and is not a location,
    /// so a thing thrown into it has no id to be moved to. That is the shape of both real
    /// observations — a rock into the dark, a key into a lava fissure.
    /// </summary>
    public static WorldState Marrow_HoldingAKey()
    {
        WorldState world = Marrow();

        world.Characters[Character.PlayerId].LocationId = "marrow-square";

        world.Locations["marrow-square"].Description =
            "A rutted market square, mostly empty. The well at its centre is boarded over, and " +
            "past the low wall on the east side the black marsh begins, going out flat and " +
            "bottomless to the horizon.";

        world.Items["iron-key"] = new Item
        {
            Id = "iron-key",
            Name = "Iron key",
            Description = "A heavy iron key, the bit worn round with use.",
            HolderId = Character.PlayerId,
        };

        return world;
    }

    /// <summary>
    /// Marrow with one plain object in the room, waiting to be looked at closely.
    ///
    /// Deliberately dull: a ring described only by its condition, so anything the prose reveals
    /// about it is new information about *what it is* rather than a restatement.
    /// </summary>
    public static WorldState Marrow_WithRing()
    {
        WorldState world = Marrow();

        world.Items["mooring-ring"] = new Item
        {
            Id = "mooring-ring",
            Name = "Rusted mooring ring",
            Description = "A heavy iron ring set into the stone, thick with rust.",
            LocationId = "marrow-tavern",
        };

        return world;
    }

    /// <summary>
    /// Marrow plus somebody nobody has named yet.
    ///
    /// The id and the name are deliberately *both* placeholders — <c>hooded-drinker</c>,
    /// "Hooded drinker" — because that is what a real anonymous introduction looks like: the
    /// extractor slugs an id from the only description available. §9 produced exactly this
    /// (<c>figure-in-cistern</c>, "Shivering figure") and then had no way to revise it.
    ///
    /// Nothing here is named "Sera", so a character or fact carrying that name can only have
    /// come from the narration under test. A fixture must not contain a plausible wrong
    /// answer that a scoring rule reads as right.
    /// </summary>
    public static WorldState Marrow_Anonymous() => AddHoodedDrinker(Marrow());

    /// <summary>
    /// <see cref="Marrow_Anonymous"/> against a world the size play reaches. A large world
    /// offers many more known ids for a rename to be misdirected at, which is the shape of
    /// failure that world size amplified for movement.
    /// </summary>
    public static WorldState Marrow_AnonymousLate() => AddHoodedDrinker(Marrow_Late());

    private static WorldState AddHoodedDrinker(WorldState world)
    {
        world.Characters["hooded-drinker"] = new Character
        {
            Id = "hooded-drinker",
            Name = "Hooded drinker",
            Description =
                "Someone sitting alone at the end of the bar with their hood up, nursing a " +
                "drink they have not touched. They have not given a name.",
            LocationId = "marrow-tavern",
            Status = "normal",
            Mood = "watchful",
        };

        return world;
    }

    /// <summary>
    /// Marrow after a long session: the same opening, grown to the size a world actually
    /// reaches in play.
    ///
    /// <b>World size is a variable the eval was not testing.</b> Every hand-written scenario
    /// runs against two locations and one fact, while a real session reported here had seven
    /// locations, six characters, forty-four facts and a 10,000-character context block — and
    /// reproduced a failure the small-world scenarios score 14/14 on. A model choosing a
    /// location id has far more plausible-looking wrong answers available in the second case.
    ///
    /// Content is invented rather than copied from anyone's save: the point is the *shape*
    /// — how many ids compete and how much context they are buried in — not the story.
    /// </summary>
    public static WorldState Marrow_Late()
    {
        WorldState world = Marrow();

        (string Id, string Name, string Description)[] places =
        [
            ("marrow-square-night", "Marrow Square at night",
                "The same rutted square under a low sky, empty and blue-black with cold."),
            // Deliberately nothing mill-shaped. An earlier version of this seed included
            // "mill-exterior" and "mill-ruins", which collide with the destination the arrival
            // scenarios walk to — the model moving to one of those scored as a *pass* under a
            // rule matching any id containing "mill", turning the substitution bug into an
            // apparent success. A fixture must not contain a plausible wrong answer that the
            // scoring rule reads as right.
            ("drowned-lane", "Drowned lane",
                "A row of half-sunk cottages on the low side of town, abandoned to the water."),
            ("peat-cuttings", "The peat cuttings",
                "Black trenches stepped down into the bog, stacked with drying turf."),
            ("well-tunnel", "The tunnel below the well",
                "A curved brick throat running off beneath the square, chest-deep in cold water."),
            ("logging-causeway", "The logging causeway",
                "A rotting timber road laid across open marsh, half of it gone to the water."),
        ];

        foreach ((string id, string name, string description) in places)
        {
            world.Locations[id] = new Location { Id = id, Name = name, Description = description };
        }

        world.Characters["guard-tomas"] = new Character
        {
            Id = "guard-tomas",
            Name = "Tomas Reed",
            Description = "A young guard, too new to the watch to hide what he thinks.",
            LocationId = "marrow-square",
            Mood = "uneasy",
        };

        world.Characters["woman-nessa"] = new Character
        {
            Id = "woman-nessa",
            Name = "Nessa",
            Description = "A village woman who has seen something she was not meant to.",
            LocationId = "well-tunnel",
            Status = "soaked",
            Mood = "frightened",
        };

        // Bulk, and deliberately so. Context length is part of what is being reproduced: a
        // few dozen remembered truths is ordinary by turn thirty, and they crowd the state
        // block the extractor has to reason over.
        string[] lore =
        [
            "The mud in the Black Fen pulls and will swallow anyone who walks it without a guide.",
            "The Cult of the Blind worships an old god called Shurus.",
            "The cult's altar stands in the sunken ruins of the old watchtower.",
            "Hald is armed with a rusted hunting knife with a bone-carved hilt.",
            "Hald knows about the Cult of the Blind.",
            "Nessa saw what was in the bucket when it came up from the well.",
            "The thing from the bucket is a tarnished silver medallion shaped like a weeping woman.",
            "The medallion feels unnaturally cold and heavy.",
            "Men from the square were looking for Nessa.",
            "The marsh claims what is owed to it.",
            "The air in the well tunnel smells of stagnant rot and something sharply metallic.",
            "The water in the well tunnel is chest-deep and cold.",
            "The tunnel forks beneath the square.",
            "One branch of the tunnel comes out among the peat cuttings.",
            "Tomas Reed is the young guard who watches Marrow Square.",
            "The causeway timbers are rotten and half of them are gone to the water.",
            "A skiff is kept tethered at the slipway.",
            "The weeping grove is where the bark turns white.",
            "Shurus promised his followers deep mud forever.",
        ];

        for (int i = 0; i < lore.Length; i++)
        {
            string id = $"lore-{i:00}";
            world.Facts[id] = new Fact { Id = id, Text = lore[i], EstablishedTurn = 1 };
            world.Characters[Character.PlayerId].Knows.Add(id);
        }

        world.Characters[Character.PlayerId].Knows.Add("well-boarded");
        world.TurnNumber = 30;

        return world;
    }
}
