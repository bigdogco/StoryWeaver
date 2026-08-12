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

    /// <summary>
    /// Display name as it appears in prose. May change; the id may not.
    ///
    /// <b>Not <c>required</c>, and the reason is about JSON rather than C#.</b> A character
    /// with a sheet gets their name from the sheet, and the design has always said
    /// <c>seed.json</c> should not repeat it. It could not: <c>required</c> makes the property
    /// mandatory in the JSON too, so a seed omitting a name was refused by the deserializer
    /// and every pack quietly wrote the name twice. Found by authoring a second world on
    /// 2026-08-12, a week after the design said otherwise.
    ///
    /// Little was given up. <c>required</c> only ever checked that the property was
    /// <i>present</i> — <c>"name": ""</c> satisfied it and produced a nameless character. The
    /// load-time checks that replace it are strictly stronger: they refuse a blank name, and
    /// they refuse a seed that names somebody whose sheet already does.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
