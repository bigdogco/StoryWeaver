namespace StoryWeaver.Core;

/// <summary>
/// Who a character is, as their author wrote them: appearance, manner, wants, and how they
/// feel about the groups and people in the world.
///
/// <b>The authored half only.</b> A sheet is pack content and never changes in play — mood,
/// status, location, knowledge and standing all live in canon. That split is the same one that
/// makes lore shippable, and the same rule the player-rename bug established: the story may
/// wound you, not redefine you.
///
/// <b>Prose, not fields.</b> The narrator consumes text either way and structured fields are
/// flattened before they reach it, so fields buy nothing for comprehension and cost
/// expressiveness — "build: heavyset" loses "wipes the same patch of counter when he is
/// thinking", which is the detail that makes a character land. Frontmatter carries only
/// <see cref="Attitudes"/>, which code needs to resolve against ids.
/// </summary>
public sealed class CharacterSheet
{
    /// <summary>Slug taken from the filename, matching the character's id in canon.</summary>
    public required string Id { get; init; }

    /// <summary>The name their author gave them, from the file's <c>#</c> heading.</summary>
    public required string Name { get; init; }

    /// <summary>Everything the narrator reads. Markdown, and may contain <c>{{ }}</c> forms
    /// referring to entities this sheet does not own.</summary>
    public required string Body { get; init; }

    /// <summary>
    /// How they feel about groups and about other people, keyed by entity id — a lore entry
    /// for an order or a people, a character id for a person.
    ///
    /// <b>The permanent why, not the current standing.</b> "Dislikes him — he stole his sword,
    /// years ago now" is history: true forever, regardless of how the relationship develops.
    /// <see cref="Character.RelationshipToPlayer"/> is a number that moves every time the story
    /// turns. He may stop disliking you; he will always be the man whose sword you stole.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attitudes { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
