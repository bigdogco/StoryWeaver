namespace StoryWeaver.Core;

// The two things the turn loop needs from a language model, expressed in Core's own
// vocabulary. Core references no HTTP client and no provider SDK; StoryWeaver.Llm supplies
// the implementations. That keeps the turn loop testable with hand-written fakes and keeps
// provider concerns from leaking into the domain.

/// <summary>Turns world state and player input into prose.</summary>
public interface INarrator
{
    Task<string> NarrateAsync(
        string context,
        string playerInput,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads a turn back into proposed changes.
///
/// Takes the player's input as well as the narration, and not for symmetry. When the player
/// writes their own actions — "*I slip the ring into Hald's palm*" — that action is
/// authoritative and has happened, but the narration may well dwell on the reaction rather
/// than restate the handover. Extracting from prose alone silently loses everything the
/// player asserted and the narrator chose not to repeat, including anything they revealed to
/// an NPC, which is a fact that NPC now knows.
/// </summary>
public interface IStateExtractor
{
    Task<ExtractionResult> ExtractAsync(
        string context,
        string playerInput,
        string narration,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// What extraction produced. Carries the raw response alongside the parsed deltas because
/// the raw text is what settles whether a bad turn was the model's fault or ours, and it is
/// unrecoverable once discarded.
/// </summary>
public sealed record ExtractionResult(IReadOnlyList<StateDelta> Deltas, string? Raw)
{
    public static ExtractionResult Empty(string? raw = null) => new([], raw);
}
