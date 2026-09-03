namespace StoryWeaver.Core;

/// <summary>
/// What a <see cref="StorySession"/> operation produced, or why it did not happen.
///
/// <b>One refusal concept, on purpose.</b> A session can decline for several unrelated reasons —
/// something else is already running, there is no turn to reroll, the turn being rerolled moved
/// canon — and before this they were expressed three different ways: a
/// <c>RerollOutcome.RefusedBecause</c>, an empty history checked by the caller, and nothing at
/// all for the case that did not exist yet. A caller had to know which kind of no it was getting.
///
/// Refusal is a <i>value</i> rather than an exception because none of these are exceptional. The
/// player pressed a button at a moment when it does not apply; that is an ordinary answer, and a
/// UI wants to show it in the same place it shows everything else.
///
/// <b>The reason is written for a person.</b> It is displayed, not matched on — no codes, no
/// enum. A client that needs to branch on <i>why</i> should be given a real API for that
/// question rather than parsing prose.
/// </summary>
public sealed record SessionResult<T>(T? Value, string? RefusedBecause)
    where T : class
{
    public static SessionResult<T> Ok(T value) => new(value, null);

    public static SessionResult<T> Refused(string because) => new(null, because);

    public bool WasRefused => RefusedBecause is not null;
}
