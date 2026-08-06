namespace StoryWeaver.Core;

/// <summary>
/// A place. Connections are deliberately a plain id list rather than a rich exit model
/// (direction, door state, locked/unlocked) — a text RPG's movement is described in prose,
/// not navigated on a compass, and the richer model is easy to add once a turn actually
/// needs it.
/// </summary>
public sealed class Location : Entity
{
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// What the place is <i>doing</i> right now — flooding, burning, filling with smoke, gone
    /// quiet. Empty for the great majority of places, which simply sit there.
    ///
    /// <b>Added because its absence was measurable.</b> A 50-turn session produced nine
    /// misfiled facts and six were one well's changing condition — <c>well-sound-changed</c>,
    /// <c>well-boards-straining</c>, <c>well-fluid-stopped</c>. Characters have
    /// <see cref="Character.Status"/> and items have <see cref="Item.Status"/>; locations had
    /// nowhere to put it, so the fact store took it, and a fact store filling with things that
    /// were true for one turn is a fact store that degrades.
    ///
    /// Same line as everywhere else in this schema: <b>condition is not identity.</b>
    /// <see cref="Description"/> is what the place is, permanently. This is what has happened
    /// to it, and it is expected to be overwritten.
    ///
    /// Defaults to empty rather than to "normal" as characters do. Most locations never have
    /// one, and rendering "status: normal" under every room would spend context on nothing.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Ids of locations reachable from here. Not automatically symmetric: a
    /// one-way drop or a locked-from-one-side door is a real thing worth representing.</summary>
    public HashSet<string> Connections { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
