namespace StoryWeaver.Core;

/// <summary>
/// The edits the delta set cannot express: rewriting a description, rewording a fact, making
/// someone forget, removing something added by mistake.
///
/// <b>The escape hatch, and deliberately the second choice.</b> Deltas are how canon changes
/// (<c>PROJECT.md</c> §3); this is what a player reaches for when no delta says what they mean.
/// It is checked after rather than validated before, because canon belongs to them and being
/// argued with is the validator's posture toward a cheap model, not toward a person.
///
/// <b>Why these are here and not in a client.</b> Removing a character and forgetting the items
/// they were holding is *policy*. A console that gets the cascade wrong and a window that gets
/// it wrong differently is exactly the drift the boundary exists to prevent — so the clients
/// choose an edit and supply arguments, and this decides what actually happens.
///
/// <b>Each returns an <see cref="Action{T}"/></b>, which is what
/// <see cref="StorySession.EditAsync"/> takes — so every edit runs inside the single-writer
/// guard and is followed by <see cref="CanonRefresh.Check"/>.
/// </summary>
public static class CanonEdits
{
    /// <summary>
    /// Rewrite what something *is*, as opposed to what has happened to it — a place, a person or
    /// a thing, since all three carry a description and none of them can have it changed by any
    /// delta except by also renaming.
    ///
    /// Silently does nothing when the id names nothing. The caller looked the entity up to offer
    /// it; a race that removes it in between is not worth an exception on a path that never
    /// refuses.
    /// </summary>
    public static Action<WorldState> Describe(string id, string description) => world =>
    {
        string text = description.Trim();

        if (world.FindCharacter(id) is { } character)
        {
            character.Description = text;
        }
        else if (world.FindLocation(id) is { } location)
        {
            location.Description = text;
        }
        else if (world.FindItem(id) is { } item)
        {
            item.Description = text;
        }
    };

    /// <summary>
    /// Reword a fact without touching who knows it.
    ///
    /// Knowledge holds fact *ids*, never text (§3) — which is exactly what makes this safe:
    /// rewording is invisible to everyone who already knows it, and nobody ends up knowing a
    /// different version.
    /// </summary>
    public static Action<WorldState> RewordFact(string factId, string text) => world =>
    {
        if (world.Facts.TryGetValue(factId, out Fact? fact))
        {
            fact.Text = text.Trim();
        }
    };

    /// <summary>
    /// Make one character forget one thing. The counterpart to <c>/knows</c>, which has no delta
    /// because nothing in a story un-learns.
    /// </summary>
    public static Action<WorldState> Forget(string characterId, string factId) => world =>
        world.FindCharacter(characterId)?.Knows.Remove(factId);

    /// <summary>
    /// Remove something from canon, repairing what has one obvious answer and leaving the rest
    /// to be reported.
    ///
    /// <b>What is repaired:</b> items held by a removed character are placed where that
    /// character was; people in a removed place become offstage, which is a legal state and what
    /// they now are; connections pointing at a removed place are dropped; a removed fact is
    /// dropped from everyone's knowledge.
    ///
    /// <b>What is not:</b> items lying in a removed place, and items held by a character who was
    /// offstage — neither has a right answer, and inventing one would move somebody's belongings
    /// somewhere they never were. <see cref="CanonRefresh.Check"/> reports both, and
    /// <see cref="ConsequencesOfRemoving"/> says so before it happens.
    /// </summary>
    public static Action<WorldState> Remove(string id) => world =>
    {
        if (world.FindCharacter(id) is { } character)
        {
            foreach (Item held in world.Items.Values.Where(i => Same(i.HolderId, id)))
            {
                // Where they stood is the only defensible place for what they were carrying.
                // Offstage means there is nowhere, so it is left held by a ghost and reported.
                if (character.LocationId is { } where)
                {
                    held.HolderId = null;
                    held.LocationId = where;
                }
            }

            world.Characters.Remove(id);
            return;
        }

        if (world.FindLocation(id) is not null)
        {
            foreach (Character stranded in world.Characters.Values.Where(c => Same(c.LocationId, id)))
            {
                stranded.LocationId = null;
            }

            foreach (Location other in world.Locations.Values)
            {
                other.Connections.RemoveWhere(c => Same(c, id));
            }

            world.Locations.Remove(id);
            return;
        }

        if (world.FindItem(id) is not null)
        {
            world.Items.Remove(id);
            return;
        }

        if (world.Facts.ContainsKey(id))
        {
            foreach (Character knower in world.Characters.Values)
            {
                knower.Knows.Remove(id);
            }

            world.Facts.Remove(id);
        }
    };

    /// <summary>
    /// What removing this would do, in the player's words, computed before anything happens.
    ///
    /// <b>This is the warning.</b> The design note that settled it: a warning shown on every edit
    /// is clicked through by the third one, and one that overstates — *"this may break your
    /// session"* — teaches people to dismiss it. So the warning is specific, true, and derived
    /// from canon: what will be moved, who will be left standing nowhere, and what will be left
    /// referring to something that no longer exists.
    ///
    /// Empty means removing this is uneventful.
    /// </summary>
    public static IReadOnlyList<string> ConsequencesOfRemoving(WorldState world, string id)
    {
        List<string> consequences = [];

        if (world.FindCharacter(id) is { } character)
        {
            if (character.IsPlayer)
            {
                consequences.Add("this is your own character — the story has nobody to happen to without them");
            }

            foreach (Item held in Ordered(world.Items.Values.Where(i => Same(i.HolderId, id)), i => i.Id))
            {
                consequences.Add(character.LocationId is { } where
                    ? $"{held.Name} ({held.Id}) is held by them, and will be left in {where}"
                    : $"{held.Name} ({held.Id}) is held by them, and they are nowhere — it will be left held by nobody");
            }

            int knownElsewhere = world.Facts.Values.Count(f => Same(f.SourceId, id));

            if (knownElsewhere > 0)
            {
                consequences.Add($"{knownElsewhere} fact(s) record them as the source; the facts stay");
            }

            return consequences;
        }

        if (world.FindLocation(id) is not null)
        {
            foreach (Character stranded in Ordered(world.Characters.Values.Where(c => Same(c.LocationId, id)), c => c.Id))
            {
                consequences.Add($"{stranded.Name} ({stranded.Id}) is there, and will become offstage");
            }

            foreach (Item lying in Ordered(world.Items.Values.Where(i => Same(i.LocationId, id)), i => i.Id))
            {
                consequences.Add($"{lying.Name} ({lying.Id}) is lying there, and will be left with nowhere to be");
            }

            int links = world.Locations.Values.Count(l => l.Connections.Any(c => Same(c, id)));

            if (links > 0)
            {
                consequences.Add($"{links} place(s) connect to it; those connections will be dropped");
            }

            return consequences;
        }

        if (world.FindItem(id) is not null)
        {
            return consequences;
        }

        if (world.Facts.ContainsKey(id))
        {
            int knowers = world.Characters.Values.Count(c => c.Knows.Contains(id));

            if (knowers > 0)
            {
                consequences.Add($"{knowers} character(s) know it, and will stop knowing it");
            }
        }

        return consequences;
    }

    /// <summary>True when the id names something this world has, of any kind.</summary>
    public static bool Exists(WorldState world, string id) =>
        world.Characters.ContainsKey(id)
        || world.Locations.ContainsKey(id)
        || world.Items.ContainsKey(id)
        || world.Facts.ContainsKey(id);

    private static bool Same(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<T> Ordered<T>(IEnumerable<T> items, Func<T, string> id) =>
        items.OrderBy(id, StringComparer.Ordinal);
}
