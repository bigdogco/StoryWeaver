namespace StoryWeaver.Core;

/// <summary>
/// One playthrough in progress: the canon it holds, and everything allowed to change it.
///
/// <b>Why this exists.</b> Until 2026-09-04 canon was owned by a local variable in the console's
/// play loop. Nothing else held a <see cref="WorldState"/> — the repository is stateless and
/// <see cref="TurnEngine"/> takes the world per call — so the domain's central object was owned
/// by a UI client, which contradicts the thin-layer rule in <c>PROJECT.md</c> §3 on the object
/// rather than on the rules.
///
/// <b>And it left a real hazard nowhere to live.</b> A turn reads canon, awaits narration and
/// extraction — twenty to sixty seconds of network — and only then mutates and saves. A console
/// cannot reach that window, because <c>Console.ReadLine</c> is blocking and nothing else can
/// start. An event-driven UI removes that accident: press Update State while narration is
/// streaming and the reload swaps a reference the in-flight turn is not holding, so the turn
/// mutates the old graph and writes pre-edit canon back over the reload. Before this class there
/// was nowhere to put a guard, because there was no object whose job was canon-for-this-session.
///
/// <b>One writer at a time, and it refuses rather than queues.</b> <see cref="SaveLock"/> already
/// decided this posture one level out: two engines on one save corrupted a 250-turn run
/// silently, and the answer was to refuse rather than to warn or to trust the caller. This is
/// the same failure one level in — two operations, one canon, no error. A queued click the
/// player has forgotten making is worse than a clear "a turn is in progress".
///
/// <b>Two write paths, deliberately asymmetric</b> (design/CANON_OWNERSHIP.md §5).
/// <see cref="AuthorAsync"/> is the norm: changes arrive as deltas, validated before they land.
/// <see cref="EditAsync"/> is the labelled escape hatch for what the delta set cannot express —
/// fixing a description, rewording a fact, removing something added by mistake — and it is
/// checked after rather than validated before, because canon belongs to the player.
///
/// <b>Reads.</b> <see cref="World"/> is exposed and is a mutable graph; the convention is that
/// reads go through it and writes go through the two methods above. That is convention rather
/// than type-level enforcement — see the note on <see cref="World"/>.
/// </summary>
public sealed class StorySession : IDisposable
{
    private readonly TurnEngine _engine;
    private readonly IWorldRepository _repository;
    private readonly LoreBook _lore;

    /// <summary>
    /// Held for the life of the session and disposed with it, but never inspected.
    ///
    /// The session owns *"this save is mine for now"* and *"I hold canon for this save"*
    /// together, because they are one lifetime — but it is an <see cref="IDisposable"/> rather
    /// than a <c>SaveLock</c> so that Core gains the ownership without learning that the
    /// mechanism is a file. Whoever opens a session acquires it; disposing the session releases
    /// it.
    /// </summary>
    private readonly IDisposable? _saveLock;

    /// <summary>
    /// The single-writer guard. Every operation that can change canon takes it, and takes it
    /// without waiting: an unavailable guard is a refusal, not a queue.
    /// </summary>
    private readonly SemaphoreSlim _oneWriter = new(1, 1);

    private WorldState _world;
    private bool _disposed;

    public StorySession(
        string saveId,
        string packId,
        WorldState world,
        TurnEngine engine,
        IWorldRepository repository,
        LoreBook? lore = null,
        IDisposable? saveLock = null)
    {
        SaveId = saveId;
        PackId = packId;
        _world = world;
        _engine = engine;
        _repository = repository;
        _lore = lore ?? LoreBook.Empty;
        _saveLock = saveLock;
    }

    /// <summary>Which playthrough this is. State, as opposed to <see cref="PackId"/>.</summary>
    public string SaveId { get; }

    /// <summary>Which world it is being played in. Content, as opposed to <see cref="SaveId"/>.</summary>
    public string PackId { get; }

    /// <summary>
    /// Canon, for reading.
    ///
    /// <b>Honest about what this is.</b> <see cref="WorldState"/> is a mutable graph by design —
    /// it is long-lived and changed a few deltas at a time, and an immutable version would mean
    /// rebuilding the world every turn. So nothing at the type level stops a caller writing
    /// through this reference and bypassing the guard entirely.
    ///
    /// The convention is: read here, write through <see cref="AuthorAsync"/> or
    /// <see cref="EditAsync"/>. Enforcing that in types needs an immutable projection of the
    /// entity graph, which is its own decision with its own cost, and is recorded as a known
    /// limit rather than pretended away.
    /// </summary>
    public WorldState World => _world;

    /// <summary>
    /// True while an operation holds the guard. For a UI to bind a spinner to, or to disable
    /// buttons with — but it is advisory, not the guard itself. Checking this and then acting
    /// is a race; the operations refuse on their own, which is the part that is safe.
    /// </summary>
    public bool IsBusy => _oneWriter.CurrentCount == 0;

    /// <summary>Play a turn: narrate, extract, validate, apply, save.</summary>
    public Task<SessionResult<TurnOutcome>> TakeTurnAsync(
        string playerInput,
        CancellationToken cancellationToken = default) =>
        GuardedAsync(
            "a turn is already in progress",
            () => _engine.RunTurnAsync(SaveId, _world, playerInput, cancellationToken));

    /// <summary>
    /// Extract the last turn again from its stored prose, for when the story was fine and only
    /// the bookkeeping failed.
    ///
    /// Takes no turn argument: *the last turn* is a session concept, and both clients were
    /// loading history themselves to find it — session work sitting in a client.
    /// </summary>
    public Task<SessionResult<TurnOutcome>> ReExtractLastAsync(
        CancellationToken cancellationToken = default) =>
        GuardedOnLastTurnAsync(
            "an extraction is already in progress",
            "there are no turns to re-extract yet",
            last => _engine.ReExtractAsync(SaveId, _world, last, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Narrate the last turn again from the same input, for when the story is wrong rather than
    /// the bookkeeping.
    ///
    /// The engine refuses a turn that moved canon, because undoing applied deltas needs a
    /// snapshot that does not exist. That refusal is folded into <see cref="SessionResult{T}"/>
    /// so a caller has one kind of no to handle rather than two.
    /// </summary>
    public async Task<SessionResult<TurnOutcome>> RerollLastAsync(
        CancellationToken cancellationToken = default)
    {
        SessionResult<RerollOutcome> guarded = await GuardedOnLastTurnAsync(
            "a narration is already in progress",
            "there are no turns to reroll yet",
            last => _engine.RerollAsync(SaveId, _world, last, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        if (guarded.WasRefused)
        {
            return SessionResult<TurnOutcome>.Refused(guarded.RefusedBecause!);
        }

        RerollOutcome reroll = guarded.Value!;

        return reroll.WasRefused
            ? SessionResult<TurnOutcome>.Refused(reroll.RefusedBecause!)
            : SessionResult<TurnOutcome>.Ok(reroll.Outcome!);
    }

    /// <summary>
    /// **Update State.** Re-read canon from disk, report what changed and anything structurally
    /// wrong with it, and adopt it.
    ///
    /// <b>The swap happens inside the guard</b>, which is the whole reason this class exists: a
    /// reload can no longer land in the middle of a turn and be silently discarded when that
    /// turn saves the graph it captured before the swap.
    ///
    /// Nothing on disk is not an error — it is a session that has not saved yet, and the
    /// in-memory world is kept.
    /// </summary>
    public Task<SessionResult<RefreshReport>> UpdateStateAsync(
        CancellationToken cancellationToken = default) =>
        GuardedAsync(
            "canon is being changed right now",
            async () =>
            {
                RefreshReport report = await CanonRefresh
                    .ReadAsync(SaveId, _world, _repository, _lore, cancellationToken)
                    .ConfigureAwait(false);

                if (report.World is { } fromDisk)
                {
                    _world = fromDisk;
                }

                return report;
            });

    /// <summary>
    /// Author canon with deltas — the ordinary way it changes, and the one to reach for first.
    ///
    /// Validated as authored before anything lands: the player's assertion is authoritative in a
    /// way an NPC's speech is not, but it still cannot introduce a dangling reference or reuse an
    /// id. Nothing accepted means nothing written.
    /// </summary>
    public Task<SessionResult<ValidationOutcome>> AuthorAsync(
        IReadOnlyList<StateDelta> deltas,
        CancellationToken cancellationToken = default) =>
        GuardedAsync(
            "canon is being changed right now",
            () => Authoring.CommitAsync(deltas, SaveId, _world, _repository, _lore, cancellationToken));

    /// <summary>
    /// **The escape hatch.** Change canon directly, for what the delta set cannot express — a
    /// description with a typo in it, a fact worded wrong, something added by mistake.
    ///
    /// <b>Checked after rather than validated before, and never refused.</b> The delta path is
    /// suspicious because a cheap model proposes deltas; this path is the player editing their
    /// own world, and being argued with is the wrong posture. The mutation is applied, canon is
    /// saved, and <see cref="CanonRefresh.Check"/> reports whatever now looks wrong.
    ///
    /// <b>The edit runs inside the guard</b>, so the hatch gets the same single-writer protection
    /// as everything else — it is a labelled exception to the delta rule, not an exception to
    /// the ownership rule.
    ///
    /// The warning belongs to the client offering this. Worth being accurate in it: editing a
    /// description corrupts nothing, and the real risk is concentrated in ids and references —
    /// change an id and everything pointing at it orphans.
    /// </summary>
    public Task<SessionResult<EditReport>> EditAsync(
        Action<WorldState> edit,
        CancellationToken cancellationToken = default) =>
        GuardedAsync(
            "canon is being changed right now",
            async () =>
            {
                edit(_world);

                await _repository.SaveAsync(SaveId, _world, cancellationToken).ConfigureAwait(false);

                return new EditReport(CanonRefresh.Check(_world, _lore));
            });

    /// <summary>
    /// Take the guard, run, release — releasing on failure as well, or one thrown exception
    /// would wedge the session shut for the rest of the run.
    ///
    /// The work returns a <see cref="SessionResult{T}"/> rather than a bare value so that an
    /// operation can decline for its own reasons from inside the guard, using the same mechanism
    /// as the busy refusal. There is one kind of no.
    /// </summary>
    private async Task<SessionResult<T>> GuardedAsync<T>(
        string busyReason,
        Func<Task<SessionResult<T>>> work)
        where T : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Zero timeout: an unavailable guard is an answer, not something to wait behind.
        if (!await _oneWriter.WaitAsync(0).ConfigureAwait(false))
        {
            return SessionResult<T>.Refused(busyReason);
        }

        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            _oneWriter.Release();
        }
    }

    /// <summary>For work that cannot decline on its own — it either produces a value or throws.</summary>
    private Task<SessionResult<T>> GuardedAsync<T>(string busyReason, Func<Task<T>> work)
        where T : class =>
        GuardedAsync(busyReason, async () => SessionResult<T>.Ok(await work().ConfigureAwait(false)));

    /// <summary>
    /// The two operations that act on the most recent turn.
    ///
    /// History is read <b>inside</b> the guard, because "the last turn" is only stable while
    /// nothing else can append one — reading it first and then taking the guard would be the
    /// same shape of bug this class exists to remove.
    /// </summary>
    private Task<SessionResult<T>> GuardedOnLastTurnAsync<T>(
        string busyReason,
        string emptyReason,
        Func<TurnRecord, Task<T>> work,
        CancellationToken cancellationToken)
        where T : class =>
        GuardedAsync<T>(busyReason, async () =>
        {
            IReadOnlyList<TurnRecord> history = await _repository
                .LoadHistoryAsync(SaveId, cancellationToken)
                .ConfigureAwait(false);

            return history.Count == 0
                ? SessionResult<T>.Refused(emptyReason)
                : SessionResult<T>.Ok(await work(history[^1]).ConfigureAwait(false));
        });

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _saveLock?.Dispose();
        _oneWriter.Dispose();
    }
}

/// <summary>
/// What a direct edit left behind: whatever <see cref="CanonRefresh.Check"/> found afterwards.
///
/// Empty is the good case, and it is a report rather than a refusal because this path never
/// refuses — the edit has already been applied and saved by the time these are read.
/// </summary>
public sealed record EditReport(IReadOnlyList<string> Warnings)
{
    public bool IsClean => Warnings.Count == 0;
}
