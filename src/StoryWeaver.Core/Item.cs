namespace StoryWeaver.Core;

/// <summary>
/// A thing. The medallion, the capstone, the rope, the seal a King's Investigator carries.
///
/// <b>Added because the model kept asking for it.</b> With no item type, an object described
/// in prose became a <see cref="Character"/> — measured 7/7, a knife standing in a tavern with
/// a name and a location, because <c>character_introduced</c> was the only delta that could
/// bring a thing into canon. Eight of eleven misfiled description-facts in one session
/// described objects, and 60% of another session's facts mentioned one: that story was about a
/// stone.
///
/// The failure that settled it: two distinct objects — a dark capstone weeping black water and
/// a bundle of pale, salt-crusted chunks — were merged into one, and canon recorded the wrong
/// one being ground to powder. With no entity to hang identity on, two things with different
/// appearances and different fates became indistinguishable.
///
/// <b>Location, not ownership, is the primary axis.</b> A design built around inventory would
/// fit "what am I carrying" and miss the actual story: that capstone was in a well, then a bog,
/// then a pool, then a mortar, and was never carried in any ordinary sense.
/// </summary>
public sealed class Item : Entity
{
    /// <summary>What it is, independent of what has happened to it.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Where it is, when nobody is holding it. Mutually exclusive with
    /// <see cref="HolderId"/> — see <see cref="IsPlaced"/>.
    /// </summary>
    public string? LocationId { get; set; }

    /// <summary>Who has it, when somebody does. Mutually exclusive with
    /// <see cref="LocationId"/>.</summary>
    public string? HolderId { get; set; }

    /// <summary>
    /// Physical condition — "intact", "ground to powder", "burned", "broken". The same
    /// distinction <see cref="Character.Status"/> draws: what has happened to it, as opposed
    /// to what it is.
    ///
    /// Grinding a stone to powder is a status change. Powder *plus salt becoming paste* is
    /// crafting, which is a system rather than a delta, and deliberately not modelled yet.
    /// </summary>
    public string Status { get; set; } = "intact";

    /// <summary>
    /// True when the item is somewhere in the world rather than in someone's hands.
    ///
    /// An item must be placed or held and never neither: "nowhere" is how an object silently
    /// stops existing while still being in canon. Enforced by <see cref="DeltaValidator"/>
    /// rather than by the type, matching how <see cref="Character.LocationId"/> is handled.
    /// </summary>
    public bool IsPlaced => LocationId is not null;

    public bool IsHeld => HolderId is not null;
}
