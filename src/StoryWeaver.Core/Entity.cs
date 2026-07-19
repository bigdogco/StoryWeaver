namespace StoryWeaver.Core;

/// <summary>
/// Anything in the world with a stable identity that narration can refer to and deltas can
/// change.
///
/// Ids are slugs (<c>marrow-tavern</c>, <c>innkeeper-hald</c>), not GUIDs. They appear in
/// prompts, in saved JSON, and in logs, and all three are things a human reads while
/// debugging. A GUID would be marginally safer against collision and considerably worse at
/// every job this id actually has.
/// </summary>
public abstract class Entity
{
    /// <summary>Stable slug. Never changes once assigned — deltas and knowledge
    /// references point at it.</summary>
    public required string Id { get; init; }

    /// <summary>Display name as it appears in prose. May change; the id may not.</summary>
    public required string Name { get; set; }
}
