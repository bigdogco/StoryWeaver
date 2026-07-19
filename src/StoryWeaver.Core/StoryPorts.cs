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

/// <summary>Reads prose back into proposed changes.</summary>
public interface IStateExtractor
{
    Task<ExtractionResult> ExtractAsync(
        string context,
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
