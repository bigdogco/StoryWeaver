namespace StoryWeaver.Core;

// The two things the turn loop needs from a language model, expressed in Core's own
// vocabulary. Core references no HTTP client and no provider SDK; StoryWeaver.Llm supplies
// the implementations. That keeps the turn loop testable with hand-written fakes and keeps
// provider concerns from leaking into the domain.

/// <summary>
/// One past turn as the narrator needs to see it: what the player wrote, and what was
/// written back. Deliberately not <see cref="TurnRecord"/> — the narrator has no business
/// seeing deltas, rejections, or raw extraction output, and handing it the whole record
/// would invite exactly the bookkeeping-while-storytelling that the two-model split exists
/// to avoid.
/// </summary>
public sealed record StoryBeat(string PlayerInput, string Narration);

/// <summary>
/// Turns world state and player input into prose.
///
/// <paramref name="recent"/> is the short-term memory, oldest first. Canon carries the
/// long-term truth — who is where, what they know, how they feel — but it cannot carry the
/// texture of the last few minutes: what an NPC actually just said, the thread of a
/// conversation, what has already been described. Without it the narrator rewrites the scene
/// from scratch every turn.
///
/// <paramref name="scenario"/> is what the story is <i>about</i> — standing context, unchanged
/// for the life of the save, empty when the pack ships none. It is passed separately from
/// <paramref name="context"/> because the two have opposite volatility: world state changes
/// every turn and belongs in the last message, a scenario never changes and belongs in the
/// stable prefix. Folding it into the context block would break prompt caching every turn to
/// resend an identical paragraph.
///
/// <b>The extractor is never given it</b> — see <see cref="IStateExtractor"/>.
/// </summary>
public interface INarrator
{
    Task<string> NarrateAsync(
        string context,
        IReadOnlyList<StoryBeat> recent,
        string playerInput,
        string scenario = "",
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
///
/// <b>Deliberately not given the scenario.</b> Same reasoning that keeps the narration window
/// out of extraction: telling it "a child has gone missing and you were sent to investigate"
/// gives it every reason to emit that premise as a <c>fact_established</c> on turn one, and
/// again whenever the prose brushes near it. The fact store already absorbs everything the
/// delta set cannot express; a standing premise in its context is an invitation.
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
public sealed record ExtractionResult(
    IReadOnlyList<StateDelta> Deltas,
    string? Raw,
    ExtractionUsage? Usage = null,
    string? Provider = null)
{
    public static ExtractionResult Empty(string? raw = null) => new([], raw);
}

/// <summary>
/// What the extraction call cost. Core's own type so the domain does not depend on a
/// provider SDK's shape.
///
/// <paramref name="ReasoningTokens"/> is a subset of <paramref name="CompletionTokens"/> and
/// is usually the bulk of it on a reasoning model — the actual delta JSON has been about 90
/// tokens against 644 spent thinking. Anyone comparing extraction models needs that split,
/// because it decides whether a pricier model is affordable.
/// </summary>
public sealed record ExtractionUsage(int PromptTokens, int CompletionTokens, int ReasoningTokens);
