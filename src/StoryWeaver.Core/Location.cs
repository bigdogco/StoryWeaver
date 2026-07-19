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

    /// <summary>Ids of locations reachable from here. Not automatically symmetric: a
    /// one-way drop or a locked-from-one-side door is a real thing worth representing.</summary>
    public HashSet<string> Connections { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
