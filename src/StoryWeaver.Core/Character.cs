using System.Text.Json.Serialization;

namespace StoryWeaver.Core;

/// <summary>
/// An NPC. The split between <see cref="Description"/> and the mutable state below is the
/// character-card distinction: description is authored once and is stable, state is what
/// the story has done to them since.
/// </summary>
public sealed class Character : Entity
{
    /// <summary>
    /// Reserved id for the player's own character.
    ///
    /// The player lives in the entity graph like anyone else. They have a location, they
    /// learn facts, and deltas need to reference them — modelling that separately would
    /// mean a parallel set of player-specific delta kinds and two code paths for every
    /// question of the form "who is here" or "who knows this".
    ///
    /// The extraction model reached for <c>characterId: "player"</c> unprompted on its
    /// first exposure to the schema, before anything told it the player was addressable.
    /// That is a decent signal about what is natural to express.
    /// </summary>
    public const string PlayerId = "player";

    /// <summary>True for the player's own character. Derived from <see cref="Entity.Id"/>,
    /// so it is not persisted — it would be a redundant column recomputable on load.</summary>
    [JsonIgnore]
    public bool IsPlayer => string.Equals(Id, PlayerId, StringComparison.OrdinalIgnoreCase);

    /// <summary>Who they are, independent of the current scene. Stable across the story.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Id of the <see cref="Location"/> they are currently in, or null if offstage
    /// and unplaced.</summary>
    public string? LocationId { get; set; }

    /// <summary>Physical or situational condition — "wounded", "asleep", "imprisoned".</summary>
    public string Status { get; set; } = "normal";

    /// <summary>Emotional register in the current moment. Changes far more freely than
    /// <see cref="Status"/>.</summary>
    public string Mood { get; set; } = "neutral";

    /// <summary>
    /// Ids of the <see cref="Fact"/>s this character knows.
    ///
    /// This is the field that makes NPCs feel simulated rather than narrated: a character
    /// who was not present cannot reference what happened. It holds ids, not text, so that
    /// two characters knowing the same fact cannot end up knowing different versions of it
    /// — which is exactly the incoherence the canon store exists to prevent.
    /// </summary>
    public HashSet<string> Knows { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How they regard the player. Meaningless on the player's own character,
    /// where it stays <see cref="Relationship.Neutral"/> and is ignored — the one wart of
    /// putting the player in the same type as everyone else, and cheaper than the parallel
    /// hierarchy that avoiding it would cost.</summary>
    public Relationship RelationshipToPlayer { get; set; } = Relationship.Neutral;

    /// <summary>Turn number when this character last appeared, or null if never.
    /// Drives "you have not seen Hald in a while" and context prioritization.</summary>
    public int? LastSeenTurn { get; set; }
}

/// <summary>
/// A character's stance toward the player. <paramref name="Standing"/> is a coarse
/// -100..100 scale for sorting and thresholds; <paramref name="Summary"/> is what actually
/// goes in a prompt, because "wary of you since the tavern" steers a model and "-30" does
/// not.
/// </summary>
public sealed record Relationship(int Standing, string Summary)
{
    public static readonly Relationship Neutral = new(0, "no strong feelings");
}
