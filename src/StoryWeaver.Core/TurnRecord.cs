namespace StoryWeaver.Core;

/// <summary>
/// One completed turn, kept whole.
///
/// This stores the rejected deltas alongside the accepted ones, and the raw extraction
/// response alongside both. That is deliberate: when canon is wrong twenty turns later, the
/// question is always "what did the model actually say, and what did we do with it" — and
/// that is unanswerable from a log of accepted changes alone. The rejections are frequently
/// the more interesting half.
/// </summary>
public sealed record TurnRecord
{
    public required int TurnNumber { get; init; }

    /// <summary>What the player typed.</summary>
    public required string PlayerInput { get; init; }

    /// <summary>The prose shown to the player.</summary>
    public required string Narration { get; init; }

    /// <summary>Deltas that passed validation and changed canon.</summary>
    public IReadOnlyList<StateDelta> Applied { get; init; } = [];

    /// <summary>Deltas that were valid but restated something already true. Not applied,
    /// and counted separately so they cannot inflate a measure of extraction quality.</summary>
    public IReadOnlyList<StateDelta> NoOps { get; init; } = [];

    /// <summary>Deltas that failed validation, with the reason. Not applied.</summary>
    public IReadOnlyList<RejectedDelta> Rejected { get; init; } = [];

    /// <summary>The extraction model's raw response, kept verbatim. Cheap to store and the
    /// only thing that settles an argument about whether a bug is in the model or in us.</summary>
    public string? RawExtraction { get; init; }

    /// <summary>
    /// Which upstream provider served the extraction call, when the gateway reported one.
    ///
    /// <b>Added because its absence made a day's measurements unreadable.</b> One model id is
    /// routed across many independent hosts running their own copies of the same weights, and
    /// they are not equivalent: on 2026-08-12 the scored set was 0/5 to 3/5 on one and 50/50
    /// clean on another within the same hour. Without this field, "canon got worse around turn
    /// thirty" has no way to become "turn thirty was served by the host that pads mood_changed
    /// until it runs out of tokens".
    ///
    /// Null when the gateway does not say. That is not an error — it is what "we do not know
    /// who served this" honestly looks like, and it is exactly the state every turn recorded
    /// before now is permanently in.
    ///
    /// Extraction only. Narration's provider would need <see cref="INarrator"/> to return more
    /// than a string, and prose quality has no score to attribute to anyone yet.
    /// </summary>
    public string? ExtractionProvider { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>A delta that did not survive validation, and why.</summary>
public sealed record RejectedDelta(StateDelta Delta, string Reason);
