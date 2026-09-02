namespace StoryWeaver.Core;

/// <summary>
/// Player-authored canon, as policy: how an id is made, when one collides, what deltas an
/// authoring act produces, and what committing them does.
///
/// Extraction deliberately never records a merely *mentioned* entity — measured at 0/7 for a
/// place the player names, a person they name, and a place the narrator names in passing. The
/// dividing line is presence, not authorship, and that is the right rule: a character saying
/// something is not the same as it being true, or every boast and lie would enter canon.
///
/// This is the door that rule does not apply to. The player is the world's author, so their
/// assertion is authoritative in a way an NPC's speech is not.
///
/// <b>Everything goes through the ordinary delta path</b> — build a <see cref="StateDelta"/>,
/// run it through <see cref="DeltaValidator"/>, apply it with <see cref="DeltaApplier"/>, save.
/// Writing to canon directly would be less code and a second way for the world to change,
/// which is how two paths start disagreeing about ids, collisions, and what was persisted.
///
/// <b>Why this is in Core rather than in the CLI, where it was written.</b> A UI is a thin
/// layer and never a driver (<c>PROJECT.md</c> §3). These rules were previously interleaved
/// with <c>Console.Write</c> calls, which meant an editor window would have reimplemented them
/// and the two copies would have drifted on exactly the things that must not drift: the shape
/// of an id, and whether a save happened. Nothing here prints, prompts, or reads a line — the
/// caller owns the conversation, this owns the consequences.
/// </summary>
public static class Authoring
{
    /// <summary>
    /// Human-readable slug, matching the ids the rest of the world uses
    /// (<c>marrow-tavern</c>, <c>innkeeper-hald</c>) rather than GUIDs.
    ///
    /// Output always satisfies <c>EntityId.IsWellFormed</c>, which lives in Storage and cannot
    /// be referenced from here. A self-test holds the two together.
    /// </summary>
    public static string Slug(string text)
    {
        System.Text.StringBuilder builder = new(text.Length);
        bool lastWasDash = false;

        foreach (char c in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasDash = false;
            }
            else if (c is '\'' or '’')
            {
                // Dropped, not treated as a separator. Apostrophes sit *inside* words —
                // "King's Investigators" must not become "king-s-investigators", and fantasy
                // names are full of them.
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        // Facts are slugged from a whole sentence, which would otherwise produce an
        // unreadable id. Four words is enough to recognise one in a save file.
        string slug = builder.ToString().Trim('-');
        string[] words = slug.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= 4 ? slug : string.Join('-', words[..4]);
    }

    /// <summary>
    /// Why this id cannot be used, or null if it is free.
    ///
    /// The validator would catch a collision anyway. This exists so the caller can say so
    /// *while the author is still typing*, instead of after they have filled in a description
    /// and lost it. Returns the reason rather than a bool because the caller — a console line
    /// or a field-level error under a text box — should not have to invent the wording.
    ///
    /// All three namespaces are checked together on purpose: facts and lore share an id space
    /// with entities, which is what lets <see cref="FactLearned"/> address a lore entry without
    /// a delta kind of its own.
    /// </summary>
    public static string? IdConflict(WorldState world, string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "An id is required.";
        }

        return world.Characters.ContainsKey(id) || world.Locations.ContainsKey(id) || world.Facts.ContainsKey(id)
            ? $"'{id}' is already used by something in this world. Choose another."
            : null;
    }

    public static IReadOnlyList<StateDelta> Place(string id, string name, string description) =>
        [new LocationIntroduced(id, name, description)];

    /// <summary>
    /// A character, optionally nowhere.
    ///
    /// <paramref name="locationId"/> null means offstage. A person you have only spoken about —
    /// a brother back home, a name from a rumour — exists without being anywhere yet, and
    /// <c>Character.LocationId</c> is nullable exactly for that. They get placed when they
    /// actually turn up.
    /// </summary>
    public static IReadOnlyList<StateDelta> Person(
        string id,
        string name,
        string description,
        string? locationId) =>
        [new CharacterIntroduced(id, name, description, locationId)];

    /// <summary>
    /// A fact, and separately whether the player's own character knows it.
    ///
    /// Establishing a fact says nothing about who knows it — that separation is the whole point
    /// of the knowledge model. An author may well write down a truth their own character has
    /// not discovered.
    /// </summary>
    public static IReadOnlyList<StateDelta> Fact(string id, string text, bool playerKnows)
    {
        List<StateDelta> deltas = [new FactEstablished(id, text)];

        if (playerKnows)
        {
            deltas.Add(new FactLearned(Character.PlayerId, id));
        }

        return deltas;
    }

    /// <summary>
    /// Rename someone already in canon — the manual counterpart to the extractor's
    /// <see cref="CharacterRenamed"/>, and the repair tool for worlds played before it existed.
    ///
    /// The id is not a parameter twice over: it is permanent by design, and an authoring
    /// surface should show it without offering to change it. The player sees that
    /// <c>figure-in-cistern</c> is now Nessa, and that this is fine.
    ///
    /// <paramref name="description"/> null keeps the existing one. A reveal often rewrites it —
    /// "a shivering figure in rags" is no longer who she is once she has a name — but not
    /// always, and making it mandatory would mean retyping a good description to change a name.
    /// </summary>
    public static IReadOnlyList<StateDelta> Rename(string characterId, string name, string? description) =>
        [new CharacterRenamed(characterId, name, description) { Evidence = "Renamed by the player." }];

    /// <summary>
    /// Grant a character knowledge of a lore entry — "Hald has heard of the Investigators".
    ///
    /// The authoring counterpart to learning one in play. It emits <see cref="FactLearned"/>
    /// against a lore id, which works because facts and lore share one id namespace; that is
    /// the same property that saves the extractor from needing a delta kind of its own.
    ///
    /// A seeded world starts with nobody having heard of anything, which is correct but
    /// unusable — an author needs to say that the innkeeper knows what the cult is without
    /// waiting for a scene to establish it.
    /// </summary>
    public static IReadOnlyList<StateDelta> Knows(string characterId, string loreId) =>
        [new FactLearned(characterId, loreId) { Evidence = "Authored by the player." }];

    /// <summary>
    /// Validate as authored, apply what survives, and persist — returning the outcome rather
    /// than reporting it. The caller decides how rejections are shown.
    ///
    /// <b>Nothing accepted means nothing written.</b> Saving an unchanged world would be
    /// harmless today and is still wrong to do: it rewrites a file the author may be editing
    /// in another window, for a change that did not happen.
    /// </summary>
    public static async Task<ValidationOutcome> CommitAsync(
        IReadOnlyList<StateDelta> deltas,
        string worldId,
        WorldState world,
        IWorldRepository repository,
        LoreBook? lore = null,
        CancellationToken cancellationToken = default)
    {
        ValidationOutcome validation = DeltaValidator.Validate(
            world, deltas, lore ?? LoreBook.Empty, authored: true);

        if (validation.Accepted.Count == 0)
        {
            return validation;
        }

        DeltaApplier.Apply(world, validation.Accepted);
        await repository.SaveAsync(worldId, world, cancellationToken).ConfigureAwait(false);

        return validation;
    }

    /// <summary>
    /// One line saying what a delta did, in the author's vocabulary rather than the schema's.
    ///
    /// Shared so a console line and a UI toast say the same thing. The fallback is the type
    /// name: this covers the kinds authoring can produce, not the whole delta set.
    /// </summary>
    public static string Summarize(StateDelta delta) => delta switch
    {
        LocationIntroduced d => $"added place {d.LocationId} ({d.Name})",
        CharacterIntroduced d => $"added character {d.CharacterId} ({d.Name})"
                                 + (d.LocationId is null ? " — offstage" : $" @ {d.LocationId}"),
        CharacterRenamed d => $"renamed {d.CharacterId} to {d.Name}"
                              + (d.Description is null ? string.Empty : ", description revised"),
        FactEstablished d => $"added fact {d.FactId}: {d.Text}",
        FactLearned d => $"{d.CharacterId} knows {d.FactId}",
        _ => delta.GetType().Name,
    };
}
