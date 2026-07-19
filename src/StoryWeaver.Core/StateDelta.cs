using System.Text.Json.Serialization;

namespace StoryWeaver.Core;

/// <summary>
/// A single proposed change to canon — the output type of the extraction pass, and the
/// contract between what a model can say and what the world can record.
///
/// <b>The set is deliberately closed.</b> Every kind is enumerated here, and extraction is
/// constrained to it by JSON schema. The alternative — a generic
/// <c>{ entity, property, value }</c> patch — buys flexibility at a price we do not want to
/// pay yet: a cheap model will confidently write <c>character.mood.current</c> when canon
/// says <c>mood</c>, and no schema can catch that, so it lands as a silent no-op.
///
/// The cost is real: a change the model wants to describe but cannot express becomes a
/// visible extraction failure. At this stage that is the point. A failure we can read in a
/// log tells us what the world model is missing; a generic patch would absorb it silently
/// and we would learn nothing.
///
/// Deltas are records because they are proposals — logged, inspected, compared, and
/// sometimes rejected — and value semantics make all of that easier. The entities they
/// apply to are mutable classes.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CharacterMoved), "character_moved")]
[JsonDerivedType(typeof(PlayerMoved), "player_moved")]
[JsonDerivedType(typeof(StatusChanged), "status_changed")]
[JsonDerivedType(typeof(MoodChanged), "mood_changed")]
[JsonDerivedType(typeof(RelationshipChanged), "relationship_changed")]
[JsonDerivedType(typeof(FactEstablished), "fact_established")]
[JsonDerivedType(typeof(FactLearned), "fact_learned")]
[JsonDerivedType(typeof(CharacterIntroduced), "character_introduced")]
[JsonDerivedType(typeof(LocationIntroduced), "location_introduced")]
public abstract record StateDelta
{
    /// <summary>
    /// Why the extraction model believes this change occurred, ideally quoting the prose.
    /// Not used to mutate anything — it exists so that when canon goes wrong we can read
    /// back what the model thought it saw, which is the difference between a fixable bug
    /// and a mystery.
    /// </summary>
    public string? Evidence { get; init; }
}

/// <summary>An existing character changed location.</summary>
public sealed record CharacterMoved(string CharacterId, string ToLocationId) : StateDelta;

/// <summary>The player changed location.</summary>
public sealed record PlayerMoved(string ToLocationId) : StateDelta;

/// <summary>Physical or situational condition changed — wounded, asleep, freed.</summary>
public sealed record StatusChanged(string CharacterId, string Status) : StateDelta;

/// <summary>Emotional register changed. Separate from <see cref="StatusChanged"/> because
/// mood turns over constantly and status rarely does; collapsing them would make every
/// flicker of feeling look like a material change to the world.</summary>
public sealed record MoodChanged(string CharacterId, string Mood) : StateDelta;

/// <summary>A character's stance toward the player changed.</summary>
public sealed record RelationshipChanged(string CharacterId, int Standing, string Summary)
    : StateDelta;

/// <summary>A new piece of world truth entered canon. Establishing a fact says nothing
/// about who knows it — that is <see cref="FactLearned"/>.</summary>
public sealed record FactEstablished(string FactId, string Text) : StateDelta;

/// <summary>A character came to know an already-established fact.</summary>
public sealed record FactLearned(string CharacterId, string FactId) : StateDelta;

/// <summary>A character not previously in canon appeared. Separate from
/// <see cref="CharacterMoved"/> so that "the narration invented someone" is distinguishable
/// from "someone walked in" — the first is worth watching closely, the second is routine.</summary>
public sealed record CharacterIntroduced(
    string CharacterId,
    string Name,
    string Description,
    string? LocationId) : StateDelta;

/// <summary>A location not previously in canon appeared.</summary>
public sealed record LocationIntroduced(
    string LocationId,
    string Name,
    string Description) : StateDelta;
