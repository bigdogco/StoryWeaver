namespace StoryWeaver.Core;

/// <summary>
/// Checks proposed deltas against canon before anything is applied.
///
/// This is not defensive boilerplate. A single probe run against one paragraph produced a
/// dangling fact reference, a re-introduction of an already-known location, a description
/// field filled with an event, and an invented character id — see docs/CHALLENGES.md. The
/// extraction model is good at *shape* and unreliable about *meaning*, so everything it
/// asserts about existing canon has to be checked.
///
/// Two rules govern the design:
///
/// <list type="number">
/// <item>Validation is sequential, not per-delta. A batch may legitimately introduce a
/// character and then move them, so each delta is checked against canon plus everything
/// accepted <i>earlier in the same batch</i>.</item>
/// <item>A rejected delta poisons whatever depends on it. If a
/// <see cref="FactEstablished"/> is rejected, a later <see cref="FactLearned"/> naming that
/// fact must fail too — otherwise the rejection would leave a dangling reference behind,
/// which is the exact failure being prevented.</item>
/// </list>
///
/// Rejections are returned, never thrown and never silently dropped. A silently discarded
/// delta is the same failure mode as a silently dropped lorebook entry: the world quietly
/// stops matching the story and nothing indicates why.
/// </summary>
public static class DeltaValidator
{
    public static ValidationOutcome Validate(WorldState world, IReadOnlyList<StateDelta> deltas)
    {
        List<StateDelta> accepted = [];
        List<StateDelta> noOps = [];
        List<RejectedDelta> rejected = [];

        // Ids that exist as far as this batch is concerned: canon, plus anything accepted so
        // far. Populated only from accepted deltas, which is what makes rejections cascade.
        HashSet<string> characters = new(world.Characters.Keys, StringComparer.OrdinalIgnoreCase);
        HashSet<string> locations = new(world.Locations.Keys, StringComparer.OrdinalIgnoreCase);
        HashSet<string> facts = new(world.Facts.Keys, StringComparer.OrdinalIgnoreCase);

        // Deltas already seen this batch, ignoring evidence text. Models pad: one observed
        // batch contained the same location_introduced three times with different quotes.
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (StateDelta delta in deltas)
        {
            if (!seen.Add(Identity(delta)))
            {
                rejected.Add(new RejectedDelta(delta, "duplicate of an earlier delta in this batch."));
                continue;
            }

            string? problem = Check(delta, characters, locations, facts);

            if (problem is not null)
            {
                rejected.Add(new RejectedDelta(delta, problem));
                continue;
            }

            // Valid, but canon already says this. Kept separate from Accepted so that
            // "extraction produced 6 changes" cannot be inflated by restatements of things
            // that were already true — which would flatter every quality measurement taken
            // over a long session.
            if (IsNoOp(world, delta))
            {
                noOps.Add(delta);
                continue;
            }

            switch (delta)
            {
                case CharacterIntroduced introduced:
                    characters.Add(introduced.CharacterId);
                    break;
                case LocationIntroduced introduced:
                    locations.Add(introduced.LocationId);
                    break;
                case FactEstablished established:
                    facts.Add(established.FactId);
                    break;
            }

            accepted.Add(delta);
        }

        return new ValidationOutcome(accepted, noOps, rejected);
    }

    /// <summary>
    /// Semantic identity, ignoring <see cref="StateDelta.Evidence"/>. Two deltas asserting
    /// the same change are the same delta even when the model quoted different prose for
    /// each, which is exactly how the padding shows up.
    /// </summary>
    private static string Identity(StateDelta delta) => delta switch
    {
        CharacterMoved d => $"moved:{d.CharacterId}:{d.ToLocationId}",
        PlayerMoved d => $"player-moved:{d.ToLocationId}",
        StatusChanged d => $"status:{d.CharacterId}:{d.Status}",
        MoodChanged d => $"mood:{d.CharacterId}:{d.Mood}",
        RelationshipChanged d => $"rel:{d.CharacterId}:{d.Standing}:{d.Summary}",
        FactEstablished d => $"fact:{d.FactId}",
        FactLearned d => $"learned:{d.CharacterId}:{d.FactId}",
        CharacterIntroduced d => $"new-char:{d.CharacterId}",
        LocationIntroduced d => $"new-loc:{d.LocationId}",
        _ => delta.ToString() ?? delta.GetType().Name,
    };

    /// <summary>
    /// True when the delta is legitimate but canon already reflects it.
    ///
    /// Measured against canon as it stood at the start of the batch, not against earlier
    /// deltas in the same batch. That is a deliberate simplification: the within-batch case
    /// (mood set twice) is already handled by duplicate detection above, and simulating the
    /// batch here would mean applying changes inside the validator — which is precisely the
    /// mixing of decision and mutation that <see cref="DeltaApplier"/> is separate to avoid.
    /// </summary>
    private static bool IsNoOp(WorldState world, StateDelta delta) => delta switch
    {
        CharacterMoved d => Same(world.FindCharacter(d.CharacterId)?.LocationId, d.ToLocationId),
        PlayerMoved d => Same(world.Player?.LocationId, d.ToLocationId),
        StatusChanged d => Same(world.FindCharacter(d.CharacterId)?.Status, d.Status),
        MoodChanged d => Same(world.FindCharacter(d.CharacterId)?.Mood, d.Mood),
        FactLearned d => world.FindCharacter(d.CharacterId)?.Knows.Contains(d.FactId) == true,
        RelationshipChanged d =>
            world.FindCharacter(d.CharacterId)?.RelationshipToPlayer is { } existing
            && existing.Standing == d.Standing
            && Same(existing.Summary, d.Summary),
        _ => false,
    };

    private static bool Same(string? a, string? b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns null when the delta is acceptable, or the reason it is not.</summary>
    private static string? Check(
        StateDelta delta,
        HashSet<string> characters,
        HashSet<string> locations,
        HashSet<string> facts)
    {
        return delta switch
        {
            CharacterIntroduced d =>
                Blank(d.CharacterId) ? "characterId is empty."
                : Blank(d.Name) ? "name is empty."
                : characters.Contains(d.CharacterId)
                    ? $"character '{d.CharacterId}' already exists. Introducing a known " +
                      "character overwrites them; use a state change instead."
                : Taken(d.CharacterId, locations, facts)
                    ? $"id '{d.CharacterId}' is already in use by a location or fact."
                : d.LocationId is { } loc && !Blank(loc) && !locations.Contains(loc)
                    ? $"location '{loc}' does not exist."
                : null,

            LocationIntroduced d =>
                Blank(d.LocationId) ? "locationId is empty."
                : Blank(d.Name) ? "name is empty."
                : locations.Contains(d.LocationId)
                    ? $"location '{d.LocationId}' already exists. Mentioning a known place " +
                      "is not introducing it."
                : Taken(d.LocationId, characters, facts)
                    ? $"id '{d.LocationId}' is already in use by a character or fact."
                : null,

            CharacterMoved d =>
                !characters.Contains(d.CharacterId) ? $"character '{d.CharacterId}' does not exist."
                : !locations.Contains(d.ToLocationId) ? $"location '{d.ToLocationId}' does not exist."
                : null,

            PlayerMoved d =>
                !characters.Contains(Character.PlayerId)
                    ? "the player character does not exist in this world."
                : !locations.Contains(d.ToLocationId) ? $"location '{d.ToLocationId}' does not exist."
                : null,

            StatusChanged d =>
                !characters.Contains(d.CharacterId) ? $"character '{d.CharacterId}' does not exist."
                : Blank(d.Status) ? "status is empty."
                : null,

            MoodChanged d =>
                !characters.Contains(d.CharacterId) ? $"character '{d.CharacterId}' does not exist."
                : Blank(d.Mood) ? "mood is empty."
                : null,

            // Standing toward yourself is not a thing. This is a direct consequence of the
            // player being an ordinary Character: the field exists on their record and has
            // no meaning there, so the validator is where that is enforced.
            RelationshipChanged d =>
                !characters.Contains(d.CharacterId) ? $"character '{d.CharacterId}' does not exist."
                : string.Equals(d.CharacterId, Character.PlayerId, StringComparison.OrdinalIgnoreCase)
                    ? "the player cannot have a relationship toward themselves."
                : d.Standing is < -100 or > 100
                    ? $"standing {d.Standing} is outside -100..100."
                : null,

            FactEstablished d =>
                Blank(d.FactId) ? "factId is empty."
                : Blank(d.Text) ? "text is empty."
                : facts.Contains(d.FactId) ? $"fact '{d.FactId}' already exists."
                : Taken(d.FactId, characters, locations)
                    ? $"id '{d.FactId}' is already in use by a character or location."
                : null,

            FactLearned d =>
                !characters.Contains(d.CharacterId) ? $"character '{d.CharacterId}' does not exist."
                : !facts.Contains(d.FactId)
                    ? $"fact '{d.FactId}' does not exist. A character cannot learn a fact that " +
                      "was never established — emit fact_established first."
                : null,

            _ => $"unhandled delta type '{delta.GetType().Name}'.",
        };
    }

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Ids must be unique across characters, locations, and facts — not merely within each.
    ///
    /// Found the hard way: extraction emitted <c>location_introduced</c> with the id
    /// <c>innkeeper-hald</c>, which exists as a character but not as a location. The per-type
    /// check passed, so a character's id was silently reused as a place. Nothing downstream
    /// would have flagged it, and the two entities would then share an identity forever.
    ///
    /// </summary>
    private static bool Taken(string id, HashSet<string> first, HashSet<string> second) =>
        first.Contains(id) || second.Contains(id);
}

/// <summary>
/// The result of validating one batch, in three categories rather than two.
///
/// <paramref name="NoOps"/> exists because "valid and changed something" and "valid but
/// already true" are different outcomes that look identical if merged, and merging them
/// makes a restatement of existing canon indistinguishable from real extraction work.
/// </summary>
public sealed record ValidationOutcome(
    IReadOnlyList<StateDelta> Accepted,
    IReadOnlyList<StateDelta> NoOps,
    IReadOnlyList<RejectedDelta> Rejected)
{
    public bool HasRejections => Rejected.Count > 0;
}
