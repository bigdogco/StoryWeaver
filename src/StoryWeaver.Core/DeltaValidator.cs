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
/// <b>Validation order is ours to choose, not the model's.</b> The batch is sorted into
/// dependency tiers before checking — declarations first, then everything that can reference
/// them. An earlier version walked the batch in the order the model happened to emit it, and
/// that quietly cost us the most common action in the game: asked to move somewhere new, the
/// extractor emits
/// <code>
/// player_moved -> old-mill
/// location_introduced old-mill
/// </code>
/// which is correct and complete, just not in our preferred order. Walking it verbatim
/// rejected the move for naming a location that "did not exist", then accepted the location
/// one line later, so exploring a new place recorded nothing. It measured 0/7 and read exactly
/// like a model failure; a prompt rule written to make the model sort its own output did not
/// fix it, because the ordering was never the model's problem to solve.
///
/// Sorting costs nothing and removes an entire class of dependence on how a model happens to
/// order its JSON — which, across providers, it does not do consistently.
///
/// Rejections are returned, never thrown and never silently dropped. A silently discarded
/// delta is the same failure mode as a silently dropped lorebook entry: the world quietly
/// stops matching the story and nothing indicates why.
/// </summary>
public static class DeltaValidator
{
    /// <param name="authored">
    /// True when these deltas came from the player through an authoring command rather than
    /// from extraction.
    ///
    /// <b>One gate, and it knows who is knocking.</b> A handful of rules exist to stop the
    /// *story* overreaching, and applying them to the player's own authoring would block the
    /// thing they are there to protect — the player must be able to set their own name, which
    /// extraction must never do. Routing authoring around the validator instead would give the
    /// world a second way to change, which is how two paths start disagreeing about ids and
    /// collisions.
    /// </param>
    public static ValidationOutcome Validate(
        WorldState world,
        IReadOnlyList<StateDelta> deltas,
        LoreBook? lore = null,
        bool authored = false)
    {
        LoreBook book = lore ?? LoreBook.Empty;

        List<StateDelta> accepted = [];
        List<StateDelta> noOps = [];
        List<RejectedDelta> rejected = [];

        // Ids that exist as far as this batch is concerned: canon, plus anything accepted so
        // far. Populated only from accepted deltas, which is what makes rejections cascade.
        HashSet<string> characters = new(world.Characters.Keys, StringComparer.OrdinalIgnoreCase);
        HashSet<string> locations = new(world.Locations.Keys, StringComparer.OrdinalIgnoreCase);
        HashSet<string> facts = new(world.Facts.Keys, StringComparer.OrdinalIgnoreCase);

        // Lore is authored content, never canon, so it is read-only here: an id may be
        // *referenced* by fact_learned and may never be created, overwritten, or shadowed.
        HashSet<string> loreIds = new(book.Ids, StringComparer.OrdinalIgnoreCase);
        HashSet<string> items = new(world.Items.Keys, StringComparer.OrdinalIgnoreCase);

        // Deltas already seen this batch, ignoring evidence text. Models pad: one observed
        // batch contained the same location_introduced three times with different quotes.
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        // Dependency order, not emission order. OrderBy is stable, so deltas within a tier
        // keep the sequence the model gave them — only the tiers move.
        foreach (StateDelta delta in deltas.OrderBy(Tier))
        {
            if (!seen.Add(Identity(delta)))
            {
                rejected.Add(new RejectedDelta(delta, "duplicate of an earlier delta in this batch."));
                continue;
            }

            string? problem = Check(delta, characters, locations, facts, loreIds, items, authored);

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
                case ItemRevealedAsCharacter promoted:
                    characters.Add(promoted.ItemId);
                    items.Remove(promoted.ItemId);
                    break;
                case LocationIntroduced introduced:
                    locations.Add(introduced.LocationId);
                    break;
                case FactEstablished established:
                    facts.Add(established.FactId);
                    break;
                case ItemIntroduced introduced:
                    items.Add(introduced.ItemId);
                    break;
            }

            accepted.Add(delta);
        }

        return new ValidationOutcome(accepted, noOps, rejected);
    }

    /// <summary>
    /// Dependency tier. Lower is validated first, so a delta is never judged against a world
    /// missing something the same batch declares.
    ///
    /// Four tiers, because the delta set is closed and its dependencies are shallow: a location
    /// depends on nothing, a character may be introduced *into* a location, an item may be
    /// placed in a location or handed to a character, and everything else references entities
    /// that exist by then.
    ///
    /// This does not weaken the cascade. Each tier sees only what the ones above it actually
    /// had <i>accepted</i>, so a rejected introduction still poisons every reference to it.
    /// </summary>
    private static int Tier(StateDelta delta) => delta switch
    {
        // Depends on nothing else in the batch.
        LocationIntroduced => 0,

        // May name a location the batch introduced above.
        // ItemRevealedAsCharacter sits here for the same reason and not one step later: the
        // turn an object proves to be a person is the turn it speaks, and the fact quoting it
        // is judged in tier 2. Putting it in the default tier would reject that fact and every
        // fact_learned behind it — which is precisely the failure this delta was built to fix.
        CharacterIntroduced or ItemRevealedAsCharacter => 1,

        // Both may name a character introduced in tier 1: a fact through its source, an item
        // through its holder.
        //
        // FactEstablished sat in tier 0 until source existed, and moving it was missed when
        // the field was added — the comment still read "depends on nothing else in the batch",
        // which had been true the day before. Live play caught it immediately: a stranger
        // speaks, is introduced and accepted, and the fact quoting them is rejected for naming
        // a character who "does not exist", taking every fact_learned with it. Sixteen of
        // twenty-three rejections in one session were this single mis-tiering.
        FactEstablished or ItemIntroduced => 2,

        // Everything that references an entity by now: moves, mood, status, relationships,
        // renames, fact_learned, and the item mutations. Picking up a thing on the turn it
        // first appears is the common case, not an edge one.
        _ => 3,
    };

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
        // Includes the speaker: two characters asserting the same thing are two claims, not a
        // duplicate. Keying on the id alone would silently drop the second half of a
        // disagreement, which is the case this field exists for.
        FactEstablished d => $"fact:{d.FactId}:{d.SourceId}",
        FactLearned d => $"learned:{d.CharacterId}:{d.FactId}",
        CharacterIntroduced d => $"new-char:{d.CharacterId}",
        ItemIntroduced d => $"new-item:{d.ItemId}",
        ItemMoved d => $"item-moved:{d.ItemId}:{d.ToLocationId}:{d.ToHolderId}",
        ItemRenamed d => $"item-rename:{d.ItemId}:{d.Name}",
        ItemStatusChanged d => $"item-status:{d.ItemId}:{d.Status}",
        CharacterRenamed d => $"rename:{d.CharacterId}:{d.Name}",
        LocationIntroduced d => $"new-loc:{d.LocationId}",
        LocationStatusChanged d => $"loc-status:{d.LocationId}:{d.Status}",
        ItemRevealedAsCharacter d => $"revealed:{d.ItemId}",
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
        // Only a no-op when it would change nothing at all. A rename carrying a revised
        // description is real work even when the name already matches — that is the shape of
        // a reveal that fills in who someone is without renaming them.
        CharacterRenamed d => world.FindCharacter(d.CharacterId) is { } target
            && Same(target.Name, d.Name)
            && (string.IsNullOrWhiteSpace(d.Description) || Same(target.Description, d.Description)),

        ItemMoved d => world.FindItem(d.ItemId) is { } item
            && Same(item.LocationId, d.ToLocationId)
            && Same(item.HolderId, d.ToHolderId),

        ItemStatusChanged d => Same(world.FindItem(d.ItemId)?.Status, d.Status),

        LocationStatusChanged d => Same(world.FindLocation(d.LocationId)?.Status, d.Status),

        ItemRenamed d => world.FindItem(d.ItemId) is { } named
            && Same(named.Name, d.Name)
            && (string.IsNullOrWhiteSpace(d.Description) || Same(named.Description, d.Description)),

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
        HashSet<string> facts,
        HashSet<string> lore,
        HashSet<string> items,
        bool authored)
    {
        return delta switch
        {
            // An item is somewhere or somebody has it. Neither is how an object silently stops
            // existing while still being in canon; both is how it ends up in two places, which
            // is the shape of the merge that produced false canon in play.
            ItemIntroduced d =>
                Blank(d.ItemId) ? "itemId is empty."
                : Blank(d.Name) ? "name is empty."
                : items.Contains(d.ItemId)
                    ? $"item '{d.ItemId}' already exists. Mentioning a known thing is not " +
                      "introducing it."
                : Taken(d.ItemId, characters, locations, facts, lore)
                    ? $"id '{d.ItemId}' is already in use by a character, location, fact or " +
                      "lore entry."
                : Placement(d.LocationId, d.HolderId, locations, characters) is { } bad ? bad
                : null,

            ItemMoved d =>
                !items.Contains(d.ItemId) ? $"item '{d.ItemId}' does not exist."
                : Placement(d.ToLocationId, d.ToHolderId, locations, characters) is { } bad ? bad
                : null,

            ItemRenamed d =>
                !items.Contains(d.ItemId) ? $"item '{d.ItemId}' does not exist."
                : Blank(d.Name) ? "name is empty."
                : string.Equals(d.Name, d.ItemId, StringComparison.OrdinalIgnoreCase)
                    ? $"name '{d.Name}' is the item's id, not a name."
                : null,

            ItemStatusChanged d =>
                !items.Contains(d.ItemId) ? $"item '{d.ItemId}' does not exist."
                : Blank(d.Status) ? "status is empty."
                : null,

            LocationStatusChanged d =>
                !locations.Contains(d.LocationId) ? $"location '{d.LocationId}' does not exist."
                : Blank(d.Status) ? "status is empty."
                : null,

            // A description is required where ItemRenamed leaves it optional. A rename may
            // legitimately change only the name; a promotion is the moment the thing stops
            // being an object, and a person carried over with an object's description reads
            // to the narrator as a person-shaped prop.
            ItemRevealedAsCharacter d =>
                !items.Contains(d.ItemId) ? $"item '{d.ItemId}' does not exist."
                : Blank(d.Name) ? "name is empty."
                : string.Equals(d.Name, d.ItemId, StringComparison.OrdinalIgnoreCase)
                    ? $"name '{d.Name}' is the id, not a name."
                : Blank(d.Description) ? "description is empty."
                : null,

            CharacterIntroduced d =>
                Blank(d.CharacterId) ? "characterId is empty."
                : Blank(d.Name) ? "name is empty."
                : characters.Contains(d.CharacterId)
                    ? $"character '{d.CharacterId}' already exists. Introducing a known " +
                      "character overwrites them; use a state change instead."
                : Taken(d.CharacterId, locations, facts, lore, items)
                    ? $"id '{d.CharacterId}' is already in use by a location, fact, lore entry or item."
                : d.LocationId is { } loc && !Blank(loc) && !locations.Contains(loc)
                    ? $"location '{loc}' does not exist."
                : null,

            LocationIntroduced d =>
                Blank(d.LocationId) ? "locationId is empty."
                : Blank(d.Name) ? "name is empty."
                : locations.Contains(d.LocationId)
                    ? $"location '{d.LocationId}' already exists. Mentioning a known place " +
                      "is not introducing it."
                : Taken(d.LocationId, characters, facts, lore, items)
                    ? $"id '{d.LocationId}' is already in use by a character, fact, lore entry or item."
                : null,

            // No uniqueness check on the name: two guards may both be "Guard", and identity
            // lives in the id regardless. The only thing that must hold is that the character
            // exists — renaming a stranger the batch never introduced is the failure worth
            // catching, since it would otherwise silently do nothing.
            // The player is not renameable. Found in play: turn 38 of a session emitted
            // character_renamed on the player, replacing the name "You" with the literal id
            // string and wiping "A traveller, recently arrived in Marrow" with a passing
            // injury. Both halves were destructive and neither was recoverable.
            //
            // Who the player is belongs to the player, not to a turn of prose. The story may
            // wound them (status_changed), move them, and teach them things; it may not decide
            // who they are.
            CharacterRenamed d =>
                !characters.Contains(d.CharacterId) ? $"character '{d.CharacterId}' does not exist."
                : Blank(d.Name) ? "name is empty."
                : !authored
                  && string.Equals(d.CharacterId, Character.PlayerId, StringComparison.OrdinalIgnoreCase)
                    ? "the player cannot be renamed by the story. Their name and description " +
                      "are the player's own — use /rename."
                // A name equal to the id is the model echoing the key back instead of writing
                // a name, which reads as a rename that "worked" and leaves a character called
                // "innkeeper-hald" in the prose.
                : string.Equals(d.Name, d.CharacterId, StringComparison.OrdinalIgnoreCase)
                    ? $"name '{d.Name}' is the character's id, not a name."
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

            // The rule that keeps "lore is authored, never extracted" true in practice rather
            // than only in intent. Extraction may record that someone *learned* an entry; it
            // may never bring one into existence, nor shadow one with a same-id fact.
            FactEstablished d =>
                Blank(d.FactId) ? "factId is empty."
                : Blank(d.Text) ? "text is empty."
                : facts.Contains(d.FactId) ? $"fact '{d.FactId}' already exists."
                : lore.Contains(d.FactId)
                    ? $"'{d.FactId}' is a lore entry. Lore is authored, not established in " +
                      "play — a character can learn it, but it cannot be created here."
                : Taken(d.FactId, characters, locations, items)
                    ? $"id '{d.FactId}' is already in use by a character, location or item."
                // A source naming nobody would be worse than no source: it reads as
                // attributed while pointing at a character who does not exist.
                : d.SourceId is { } speaker && !Blank(speaker) && !characters.Contains(speaker)
                    ? $"source '{speaker}' is not a character in this world."
                    // Lore is checked above with a more specific message, so it is
                    // deliberately absent from this Taken call.
                : null,

            // Accepts a lore id as readily as a fact id. One namespace is what lets learning
            // lore in play reuse this delta instead of needing a kind of its own.
            FactLearned d =>
                !characters.Contains(d.CharacterId) ? $"character '{d.CharacterId}' does not exist."
                : !facts.Contains(d.FactId) && !lore.Contains(d.FactId)
                    ? $"fact '{d.FactId}' does not exist. A character cannot learn a fact that " +
                      "was never established — emit fact_established first."
                : null,

            _ => $"unhandled delta type '{delta.GetType().Name}'.",
        };
    }

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Where an item is, checked as one rule because the two fields are one decision.
    ///
    /// Returns null when the placement is legitimate, or the reason it is not. Shared by
    /// introduction and movement so the invariant cannot hold at one door and not the other.
    /// </summary>
    private static string? Placement(
        string? locationId,
        string? holderId,
        HashSet<string> locations,
        HashSet<string> characters)
    {
        bool placed = !Blank(locationId);
        bool held = !Blank(holderId);

        return (placed, held) switch
        {
            (false, false) =>
                "an item must be in a location or held by a character. Neither was given, and " +
                "an item that is nowhere has silently stopped existing.",
            (true, true) =>
                $"an item cannot be both in '{locationId}' and held by '{holderId}'. Give one.",
            (true, false) when !locations.Contains(locationId!) =>
                $"location '{locationId}' does not exist.",
            (false, true) when !characters.Contains(holderId!) =>
                $"character '{holderId}' does not exist.",
            _ => null,
        };
    }

    /// <summary>
    /// Ids must be unique across characters, locations, facts and lore — not merely within
    /// each.
    ///
    /// Found the hard way: extraction emitted <c>location_introduced</c> with the id
    /// <c>innkeeper-hald</c>, which exists as a character but not as a location. The per-type
    /// check passed, so a character's id was silently reused as a place. Nothing downstream
    /// would have flagged it, and the two entities would then share an identity forever.
    ///
    /// Lore joins the same namespace, which is what allows <see cref="Character.Knows"/> to
    /// hold both fact and lore ids without ambiguity.
    /// </summary>
    private static bool Taken(string id, params HashSet<string>[] namespaces) =>
        namespaces.Any(set => set.Contains(id));
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
