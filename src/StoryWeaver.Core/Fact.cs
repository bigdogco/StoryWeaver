namespace StoryWeaver.Core;

/// <summary>
/// A discrete piece of world truth that characters can know, separately from whether it is
/// true. "The innkeeper poisoned the ale" is a fact whether or not anyone has discovered it.
///
/// Deliberately <b>not</b> an <see cref="Entity"/>. Entities have a name distinct from their
/// description; a fact is only its text, and giving it a <c>Name</c> would invite the
/// extraction model to invent titles for statements. The id exists solely so
/// <see cref="Character.Knows"/> can reference it.
/// </summary>
public sealed class Fact
{
    public required string Id { get; init; }

    /// <summary>The claim itself, as a single sentence. Kept short deliberately: facts are
    /// replayed into prompts, and a paragraph-length "fact" is really several facts that
    /// characters should be able to know independently.</summary>
    public required string Text { get; set; }

    /// <summary>Turn on which this entered canon. Useful for ordering and for spotting
    /// facts the extraction pass invented late in a session.</summary>
    public int EstablishedTurn { get; init; }
}
