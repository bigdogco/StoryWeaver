using StoryWeaver.Core;

namespace StoryWeaver.Cli;

/// <summary>
/// The hardcoded starting world, shared by the play harness and the extraction eval.
///
/// Shared deliberately: an eval that scores against a different world than the one being
/// played is measuring the wrong thing, and two copies of a seed drift the moment one is
/// edited.
/// </summary>
internal static class WorldSeeds
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
