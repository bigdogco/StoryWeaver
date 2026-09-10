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
    /// Things whose lifetime is exactly this session's: the save lock, and the provider client
    /// the turn engine talks through. Held and disposed, never inspected.
    ///
    /// <b>Untyped on purpose.</b> The session owns *"this save is mine for now"* and *"I hold
    /// canon for this save"* together, because they are one lifetime — but as
    /// <see cref="IDisposable"/> rather than a <c>SaveLock</c> and an <c>HttpClient</c>, so Core
    /// gains the ownership without learning that one is a file and the other is a socket.
    /// Whoever opens a session acquires them; disposing the session releases them.
    /// </summary>
    private readonly IReadOnlyList<IDisposable> _owned;

    /// <summary>
    /// The single-writer guard. Every operation that can change canon takes it, and takes it
    /// without waiting: an unavailable guard is a refusal, not a queue.
    /// </summary>
    private readonly SemaphoreSlim _oneWriter = new(1, 1);

    private WorldState _world;
    private bool _disposed;
    private readonly Guid _canonFormSession = Guid.NewGuid();

    public StorySession(
        string saveId,
        string packId,
        WorldState world,
        TurnEngine engine,
        IWorldRepository repository,
        LoreBook? lore = null,
        IReadOnlyList<IDisposable>? owned = null)
    {
        SaveId = saveId;
        PackId = packId;
        _world = world;
        _engine = engine;
        _repository = repository;
        _lore = lore ?? LoreBook.Empty;
        _owned = owned ?? [];
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

    public WorldView ProjectWorld(bool authorView = false) => authorView
        ? new WorldView([.. AuthorProjection.Characters(_world), .. AuthorProjection.Locations(_world),
            .. AuthorProjection.Facts(_world), .. AuthorProjection.Items(_world),
            .. _lore.All.Select(l => new WorldViewEntry(CanonKind.Fact, l.Id, l.Title, "Private lore reference", l.Body, [], CanAuthor: false))])
        : DiscoveryProjection.Player(_world, _lore);

    /// <summary>
    /// True while an operation holds the guard. For a UI to bind a spinner to, or to disable
    /// buttons with — but it is advisory, not the guard itself. Checking this and then acting
    /// is a race; the operations refuse on their own, which is the part that is safe.
    /// </summary>
    public bool IsBusy => _oneWriter.CurrentCount == 0;

    /// <summary>
    /// The tail of the story, oldest first — for replaying a resumed session, or showing the
    /// last raw extraction.
    ///
    /// <b>Not guarded, deliberately.</b> History is append-only, so the worst a concurrent read
    /// can see is a tail one turn shorter than it will be a moment later. Taking the guard for
    /// a read would mean a UI refreshing a transcript could be told "a turn is in progress",
    /// which is useless — and it would make the guard contended by things that never write.
    ///
    /// <b>A read rather than a repository.</b> Exposing <c>IWorldRepository</c> would hand every
    /// client a way to write behind the guard's back, which is the whole thing this class
    /// exists to prevent.
    /// </summary>
    public async Task<IReadOnlyList<TurnRecord>> RecentTurnsAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return [];
        }

        IReadOnlyList<TurnRecord> history = await _repository
            .LoadHistoryAsync(SaveId, cancellationToken)
            .ConfigureAwait(false);

        return history.Count <= count ? history : [.. history.Skip(history.Count - count)];
    }

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

    /// <summary>Check current canon with this session's lore, without reloading or writing it.</summary>
    public Task<SessionResult<EditReport>> CheckCanonAsync() =>
        GuardedAsync(
            "canon is being changed right now",
            () => Task.FromResult(new EditReport(CanonRefresh.Check(_world, _lore))));

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

                await SaveAuthoredWorldAsync(cancellationToken).ConfigureAwait(false);

                return new EditReport(CanonRefresh.Check(_world, _lore));
            });

    public Task<SessionResult<CanonEditSnapshot>> BeginCanonEditAsync(CanonTarget target) =>
        GuardedAsync<CanonEditSnapshot>("canon is being changed right now", () => Task.FromResult(
            CanonCorrection.Capture(_world, target) is { } snapshot
                ? SessionResult<CanonEditSnapshot>.Ok(snapshot)
                : SessionResult<CanonEditSnapshot>.Refused("The selected entity no longer exists with the same identity.")));

    private Task SaveAuthoredWorldAsync(CancellationToken cancellationToken)
    {
        _world.Discovery ??= DiscoveryState.Minimal(_world);
        return _repository.SaveAsync(SaveId, _world, cancellationToken);
    }

    public Task<SessionResult<DiscoveryEditSnapshot>> BeginDiscoveryEditAsync() =>
        GuardedAsync("canon is being changed right now", () => Task.FromResult(new DiscoveryEditSnapshot(_world, _canonFormSession)));

    public Task<SessionResult<EditReport>> SaveDiscoveryAsync(DiscoveryEditSnapshot baseline, DiscoveryState draft,
        CancellationToken cancellationToken = default) => GuardedAsync<EditReport>("canon is being changed right now", async () =>
        {
            if (baseline.Session != _canonFormSession || baseline.Revision != DiscoveryEditSnapshot.Fingerprint(_world))
                return SessionResult<EditReport>.Refused("Canon or player knowledge changed while this form was open. Reopen it before saving.");
            var candidate = CanonCreation.Copy(_world);
            candidate.Discovery = DiscoveryAuthoring.Prepare(_world, draft);
            var errors = DiscoveryIntegrity.Check(candidate);
            if (errors.Count > 0) return SessionResult<EditReport>.Refused(string.Join("\n", errors));
            _world.Discovery = candidate.Discovery;
            await SaveAuthoredWorldAsync(cancellationToken).ConfigureAwait(false);
            return SessionResult<EditReport>.Ok(new EditReport(CanonRefresh.Check(_world, _lore)));
        });

    /// <summary>Capture a detached form/catalog baseline belonging to this session.</summary>
    public Task<SessionResult<CanonCreationSnapshot>> BeginCanonCreationAsync(CanonKind kind) =>
        GuardedAsync("canon is being changed right now", () => Task.FromResult(
            new CanonCreationSnapshot(_world, _lore, kind, _canonFormSession)));

    /// <summary>Validate and apply the complete authored form, then save once without advancing the story.</summary>
    public Task<SessionResult<EditReport>> CreateCanonAsync(CanonCreationSnapshot baseline, string id, CanonFields fields,
        CancellationToken cancellationToken = default, InitialDiscovery? initialDiscovery = null) =>
        GuardedAsync<EditReport>("canon is being changed right now", async () =>
        {
            if (baseline.Session != _canonFormSession) return SessionResult<EditReport>.Refused("This form belongs to another playthrough.");
            if (CanonCreation.Validate(_world, _lore, baseline, id, fields) is { } error)
                return SessionResult<EditReport>.Refused(error);
            var deltas = CanonCreation.Deltas(id, fields);
            var validation = DeltaValidator.Validate(_world, deltas, _lore, authored: true);
            if (validation.Rejected.Count != 0)
                return SessionResult<EditReport>.Refused(string.Join("\n", validation.Rejected.Select(r => r.Reason)));
            // Validate the final form as a whole too: a future applier/no-op change must not
            // turn a successful Add into a partially represented draft.
            var candidate = CanonCreation.Copy(_world);
            DeltaApplier.Apply(candidate, validation.Accepted);
            CanonCreation.ApplyFields(candidate, id, fields);
            DiscoveryAuthoring.InitializeEntity(candidate, baseline.Kind, id, initialDiscovery);
            var discoveryErrors = DiscoveryIntegrity.Check(candidate);
            if (discoveryErrors.Count > 0) return SessionResult<EditReport>.Refused(string.Join("\n", discoveryErrors));
            var created = CanonCorrection.Capture(candidate, new(baseline.Kind, id, id));
            if (created is null || !CanonCorrection.Same(created.Fields, fields))
                return SessionResult<EditReport>.Refused("The complete form could not be applied. Nothing was added.");
            DeltaApplier.Apply(_world, validation.Accepted);
            CanonCreation.ApplyFields(_world, id, fields);
            _world.Discovery = candidate.Discovery;
            await SaveAuthoredWorldAsync(cancellationToken).ConfigureAwait(false);
            return SessionResult<EditReport>.Ok(new EditReport(CanonRefresh.Check(_world, _lore)));
        });

    /// <summary>Describe the precise removal effects without changing canon.</summary>
    public Task<SessionResult<CanonRemovalPlan>> PreviewCanonRemovalAsync(CanonTarget target) =>
        GuardedAsync<CanonRemovalPlan>("canon is being changed right now", () => Task.FromResult(
            CanonRemoval.Capture(_world, _lore, target, _canonFormSession) is { } plan
                ? SessionResult<CanonRemovalPlan>.Ok(plan)
                : SessionResult<CanonRemovalPlan>.Refused("The selected entity no longer exists with the same identity.")));

    /// <summary>Apply only the reviewed plan; a changed plan is returned for a new confirmation without writing.</summary>
    public Task<SessionResult<CanonRemovalOutcome>> RemoveCanonAsync(CanonRemovalPlan baseline,
        CancellationToken cancellationToken = default) =>
        GuardedAsync<CanonRemovalOutcome>("canon is being changed right now", async () =>
        {
            if (baseline.Session != _canonFormSession) return SessionResult<CanonRemovalOutcome>.Refused("This preview belongs to another playthrough.");
            var current = CanonRemoval.Capture(_world, _lore, baseline.Target, _canonFormSession);
            if (current is null) return SessionResult<CanonRemovalOutcome>.Refused("The selected entity no longer exists with the same identity. Close this preview.");
            if (!ReferenceEquals(current.Identity, baseline.Identity))
                return SessionResult<CanonRemovalOutcome>.Refused("The selected entry was replaced after this preview opened. Close and reopen Remove to review its identity.");
            if (current.RefusedBecause is { } reason) return SessionResult<CanonRemovalOutcome>.Refused(reason);
            if (current.Revision != baseline.Revision || !current.Dependencies.SequenceEqual(baseline.Dependencies, ReferenceEqualityComparer.Instance))
                return SessionResult<CanonRemovalOutcome>.Ok(new(null, current));
            CanonRemoval.Apply(_world, current);
            await SaveAuthoredWorldAsync(cancellationToken).ConfigureAwait(false);
            return SessionResult<CanonRemovalOutcome>.Ok(new(new EditReport(CanonRefresh.Check(_world, _lore)), null));
        });

    /// <summary>A complete typed correction, checked after and saved once under the same guard as direct edits.</summary>
    public Task<SessionResult<EditReport>> EditAsync(CanonEditSnapshot baseline, CanonFields fields,
        CancellationToken cancellationToken = default) =>
        GuardedAsync<EditReport>("canon is being changed right now", async () =>
        {
            if (CanonCorrection.Validate(_world, baseline, fields) is { } reason)
                return SessionResult<EditReport>.Refused(reason);
            if (CanonCorrection.Same(baseline.Fields, fields))
                return SessionResult<EditReport>.Ok(new EditReport(CanonRefresh.Check(_world, _lore)));
            CanonCorrection.Apply(_world, baseline, fields);
            await SaveAuthoredWorldAsync(cancellationToken).ConfigureAwait(false);
            return SessionResult<EditReport>.Ok(new EditReport(CanonRefresh.Check(_world, _lore)));
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

        foreach (IDisposable owned in _owned)
        {
            owned.Dispose();
        }

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
