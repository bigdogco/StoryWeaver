using StoryWeaver.Core;
using StoryWeaver.Llm;
using StoryWeaver.Storage;

namespace StoryWeaver.App;

/// <summary>
/// What opening a session produced. Exactly one of three things happened.
///
/// <b>Why this is a returned value rather than printed output.</b> Opening a session makes a
/// dozen decisions and produces three quite different situations, and every one of them was
/// previously expressed by writing to the console in the middle of a load. A window needs the
/// same decisions and a different rendering, so the decisions come back as data.
/// </summary>
public sealed record SessionOpening
{
    private SessionOpening()
    {
    }

    /// <summary>The session, when there is one. Null in the other two cases.</summary>
    public StorySession? Session { get; private init; }

    /// <summary>
    /// Set when the pack does not say who the player is and somebody has to be asked.
    ///
    /// Everything is already loaded and the save lock is already held; completing this costs
    /// one write. It is <see cref="IDisposable"/> because a client that abandons the question —
    /// the player closes the dialog — must be able to give the save back.
    /// </summary>
    public PendingPlayer? NeedsPlayer { get; private init; }

    /// <summary>
    /// Why the save could not be opened, written for a person. Today there is one cause: it is
    /// already held by a live session.
    /// </summary>
    public string? RefusedBecause { get; private init; }

    /// <summary>Who holds the save, when <see cref="RefusedBecause"/> says it is held.</summary>
    public string? HeldBy { get; private init; }

    /// <summary>
    /// Everything a client needs to render the opening, gathered once rather than recomputed.
    /// Present whenever the save was opened at all — including the needs-a-player case, because
    /// the banner is worth showing before the question.
    /// </summary>
    public SessionContext? Context { get; private init; }

    public bool WasRefused => RefusedBecause is not null;

    public bool IsWaitingForPlayer => NeedsPlayer is not null;

    internal static SessionOpening Opened(StorySession session, SessionContext context) =>
        new() { Session = session, Context = context };

    internal static SessionOpening AwaitingPlayer(PendingPlayer pending, SessionContext context) =>
        new() { NeedsPlayer = pending, Context = context };

    internal static SessionOpening Refused(string because, string? heldBy = null) =>
        new() { RefusedBecause = because, HeldBy = heldBy };
}

/// <summary>
/// The facts about a session that a client renders but never decides: which pack, whether this
/// is a resume, where the log went, whether the pack has moved under the save.
///
/// Gathered here so a banner is a rendering of one object rather than eight arguments assembled
/// at each call site — which is how two clients end up disagreeing about what a session is.
/// </summary>
public sealed record SessionContext(
    WorldPack Pack,
    PromptLibrary Prompts,
    string LogPath,
    string SaveRootDirectory,
    bool Resumed,
    int TurnNumber,
    int HistoryTurns)
{
    /// <summary>
    /// Set when the save was started against a different version of this pack.
    ///
    /// A note rather than a problem: content may have moved, anything the pack no longer defines
    /// stays in the world, and nothing is removed. Reported rather than printed so a window can
    /// put it somewhere other than a scrollback.
    /// </summary>
    public string? PackVersionAtStart { get; init; }

    public bool PackHasMoved => PackVersionAtStart is not null;
}
