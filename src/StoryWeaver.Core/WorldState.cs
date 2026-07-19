namespace StoryWeaver.Core;

/// <summary>
/// The entity graph plus the turn counter. This is canon — the source of truth that prose
/// is a rendering of, never the other way round.
///
/// Mutable by design. It is a long-lived graph changed transactionally a few deltas at a
/// time; an immutable version would mean rebuilding the whole world every turn to buy
/// value semantics nothing here wants.
///
/// Applying deltas lives in the turn loop, not here. This type holds state and answers
/// questions about it; deciding whether a proposed change is legitimate is a separate
/// concern with different failure modes.
/// </summary>
public sealed class WorldState
{
    /// <summary>Turns completed. Deltas stamp facts with this, and it drives
    /// <see cref="Character.LastSeenTurn"/>.</summary>
    public int TurnNumber { get; set; }

    /// <summary>
    /// The player's own character, or null before the opening scene is set.
    ///
    /// The player is an ordinary entry in <see cref="Characters"/> under
    /// <see cref="Character.PlayerId"/>, not a separate field. That matters more than it
    /// looks: an earlier draft kept a standalone <c>PlayerLocationId</c> alongside the
    /// character graph, which is the same fact stored twice and therefore the same fact
    /// able to disagree with itself after any delta that touched only one of them.
    /// </summary>
    public Character? Player => FindCharacter(Character.PlayerId);

    /// <summary>Where the player currently is. Derived — the player's own
    /// <see cref="Character.LocationId"/> is the only copy.</summary>
    public string? PlayerLocationId => Player?.LocationId;

    public Dictionary<string, Character> Characters { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, Location> Locations { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, Fact> Facts { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Character? FindCharacter(string id) =>
        Characters.TryGetValue(id, out Character? character) ? character : null;

    public Location? FindLocation(string id) =>
        Locations.TryGetValue(id, out Location? location) ? location : null;

    public Fact? FindFact(string id) =>
        Facts.TryGetValue(id, out Fact? fact) ? fact : null;

    /// <summary>Everyone currently in a given location, <b>including the player</b> if they
    /// are there. Use <see cref="NpcsWithPlayer"/> for scene population.</summary>
    public IEnumerable<Character> CharactersIn(string locationId) =>
        Characters.Values.Where(c =>
            string.Equals(c.LocationId, locationId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// NPCs in the room with the player — the basis of "who is in the scene", which is what
    /// context assembly needs first.
    ///
    /// Excludes the player explicitly. Now that they are an ordinary character, every query
    /// meaning "the people around you" has to say so, or the player quietly turns up in
    /// their own scene description.
    /// </summary>
    public IEnumerable<Character> NpcsWithPlayer() =>
        PlayerLocationId is null
            ? []
            : CharactersIn(PlayerLocationId).Where(c => !c.IsPlayer);

    /// <summary>The facts a given character knows, resolved to their text.
    ///
    /// Silently skips ids with no matching fact rather than throwing. A dangling reference
    /// is a real possibility once extraction is inventing ids, and losing a turn over one
    /// is a worse outcome than proceeding with slightly less knowledge — but it is a
    /// symptom, so the turn loop validates references rather than relying on this.</summary>
    public IEnumerable<Fact> KnownFacts(Character character) =>
        character.Knows
            .Select(FindFact)
            .Where(f => f is not null)
            .Select(f => f!);
}
