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

    /// <summary>
    /// Who asserted this, or null when the narration stated it as plain world truth.
    ///
    /// <b>Attribution, not a truth value.</b> A boolean "is this true" would ask the extractor
    /// to adjudicate honesty from a single turn, which it cannot do and should not try. A
    /// speaker is an observable: whether Hald said it is checkable from the prose, and whether
    /// he was lying is something the story resolves later — with the player as the right
    /// arbiter.
    ///
    /// This is what lets canon hold two contradictory claims without being wrong. Hald says
    /// the stone went to the quarry, Mabb says the deep bog; both are recorded, neither is
    /// asserted, and the disagreement becomes content rather than corruption.
    ///
    /// Immutable once set. Who said a thing does not change; a second character saying the
    /// same thing is a separate fact, or simply <see cref="Character.Knows"/> growing.
    /// </summary>
    public string? SourceId { get; init; }

    /// <summary>Turn on which this entered canon. Useful for ordering and for spotting
    /// facts the extraction pass invented late in a session.</summary>
    public int EstablishedTurn { get; init; }
}
