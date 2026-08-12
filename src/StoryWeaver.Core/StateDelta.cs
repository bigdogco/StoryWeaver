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
// Serialization goes through StateDeltaConverter (registered in StoryJson), NOT through
// [JsonPolymorphic]. The built-in support requires the "kind" discriminator to be the first
// property in the object, and models do not reliably put it there — one provider emitted
// properties alphabetically, which put "kind" last and broke every delta. The kind/type map
// lives in that converter; adding a delta kind means editing it, DeltaApplier, and
// DeltaSchema together.
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

/// <summary>
/// A character already in canon is now known by a different name — most often because the
/// story revealed the identity of someone introduced anonymously.
///
/// <b>The id never changes.</b> Only the name does, so every existing reference — a
/// character's <c>Knows</c>, the turn history, anything future lore adds — stays valid for
/// free. Ids are internal: the narrator is sent names only, so a stale-looking id costs
/// nothing, while rewriting one would mean rewriting every reference to buy readability in
/// a file the player never sees.
///
/// <see cref="Description"/> is optional because a reveal usually revises both — "a
/// shivering figure in rags" becomes "Nessa, a young woman from the village" — but a bare
/// name reveal must not be forced to invent one.
///
/// Added after §9: a character introduced anonymously kept that name for 36 turns while
/// the prose called her something else, and the extractor, having no way to say this,
/// stored her real name as a *fact* instead.
/// </summary>
public sealed record CharacterRenamed(
    string CharacterId,
    string Name,
    string? Description) : StateDelta;

/// <summary>
/// A new piece of world truth entered canon. Establishing a fact says nothing about who knows
/// it — that is <see cref="FactLearned"/>.
///
/// <paramref name="SourceId"/> is who asserted it, or null when the narration states it as
/// plain truth. **Not a truth value.** A boolean would ask the extractor to adjudicate
/// honesty, which it cannot do from one turn; a speaker is an observable — "Hald said X" is
/// checkable from the prose, and whether X is true is a thing the story resolves later.
///
/// Added after a turn in which two characters answered the same question differently and both
/// answers were stored as settled fact. They cannot both be true, and without a speaker canon
/// could not say which was contested. The model had been improvising around the gap for weeks,
/// writing "claims" into fact text unprompted.
/// </summary>
public sealed record FactEstablished(string FactId, string Text, string? SourceId = null)
    : StateDelta;

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

/// <summary>
/// A thing not previously in canon entered it.
///
/// Exactly one of <paramref name="LocationId"/> and <paramref name="HolderId"/> must be set —
/// an item is somewhere or somebody has it, and "nowhere" is how an object silently stops
/// existing while still being in canon.
/// </summary>
public sealed record ItemIntroduced(
    string ItemId,
    string Name,
    string Description,
    string? LocationId,
    string? HolderId) : StateDelta;

/// <summary>
/// An item changed hands, was picked up, or was put down.
///
/// One delta rather than separate take/drop/give kinds, because they are the same operation:
/// the item stops being where it was and starts being somewhere else. Exactly one target must
/// be set.
/// </summary>
public sealed record ItemMoved(
    string ItemId,
    string? ToLocationId,
    string? ToHolderId) : StateDelta;

/// <summary>
/// An item is now known to be something other than what it was taken for — "old foundation
/// blocks" turning out to be a carved capstone.
///
/// The same problem <see cref="CharacterRenamed"/> solves, and the same answer: the id never
/// changes, so every reference survives the revelation.
/// </summary>
public sealed record ItemRenamed(
    string ItemId,
    string Name,
    string? Description) : StateDelta;

/// <summary>
/// An item's physical condition changed — ground to powder, burned, broken.
///
/// The <see cref="StatusChanged"/> of objects, and drawn for the same reason: what has happened
/// to a thing is not what the thing is, and collapsing them means an event overwrites a
/// description.
/// </summary>
public sealed record ItemStatusChanged(string ItemId, string Status) : StateDelta;

/// <summary>
/// An object left the world for good — destroyed, burned, swallowed, thrown where nothing will
/// get it back.
///
/// <b>The counterpart to the rule that an item is somewhere or held by somebody.</b> That rule
/// is right and stays: an item that is merely *nowhere* has silently stopped existing, which is
/// the quietest way for an object to vanish. But some things genuinely stop existing, and
/// without a way to say so the model says it the only way it can — <c>item_moved</c> to nothing,
/// which is refused, leaving canon asserting the old placement. A key hurled into a lava
/// fissure stayed recorded as lying in a cellar, retrievable, for the rest of the session.
///
/// <b>The item is removed rather than flagged.</b> Canon is what is true now, and a destroyed
/// thing is not part of the world; the turn history keeps the record of it having been. Nothing
/// structural points at an item — knowledge is keyed to facts and lore — so removal breaks no
/// reference.
///
/// <paramref name="Reason"/> is not decoration. "Gone" and "gone because you threw it in the
/// marsh" are different things to a narrator reading the turn log, and this is the only place
/// the second one survives.
/// </summary>
public sealed record ItemLost(string ItemId, string Reason) : StateDelta;

/// <summary>
/// An object turned out to be a person.
///
/// <b>The id is kept.</b> Ids are already unique across every namespace, so the entity moves
/// from <see cref="WorldState.Items"/> to <see cref="WorldState.Characters"/> without any
/// reference to it breaking. The same principle as <see cref="CharacterRenamed"/>: a revelation
/// changes what we know a thing is, never which thing it is.
///
/// <b>Why a delta of its own rather than an introduction plus a removal.</b> Two deltas would
/// mean a batch that half-applies leaves either a man and a shape in the same room, or nothing
/// at all. Extraction measured exactly that: given no promotion, models introduce a new
/// character and leave the item lying there, in a room that now contains both.
///
/// Found in play, four times in one session. A shape under a tarp was introduced as an item —
/// correctly — and then proved to be a living man. Every attempt to give it a character's
/// status was rejected, and the rejection of a fact naming it as the speaker took three
/// <c>fact_learned</c> with it, so the revelation never reached canon at all.
/// </summary>
public sealed record ItemRevealedAsCharacter(
    string ItemId,
    string Name,
    string Description) : StateDelta;

/// <summary>
/// A place's condition changed — the water rising, the fire taking hold, the noise stopping.
///
/// The <see cref="StatusChanged"/> of places. Last of the three, and the gap was measurable:
/// six of nine misfiled facts in one session were a single well's changing state, filed as
/// permanent truths because nothing else in the schema would hold them.
/// </summary>
public sealed record LocationStatusChanged(string LocationId, string Status) : StateDelta;

/// <summary>A location not previously in canon appeared.</summary>
public sealed record LocationIntroduced(
    string LocationId,
    string Name,
    string Description) : StateDelta;
