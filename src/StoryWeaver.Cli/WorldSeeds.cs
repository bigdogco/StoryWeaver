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
}
