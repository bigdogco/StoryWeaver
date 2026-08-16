namespace StoryWeaver.Core;

/// <summary>
/// One turn: assemble context, narrate, extract, validate, commit.
///
/// The ordering that matters: <b>narration is shown to the player regardless of what
/// extraction does.</b> Extraction failing is a canon problem, not a storytelling problem,
/// and discarding good prose because a second model could not parse it would turn a silent
/// bookkeeping error into a visible broken game. The turn still reports the failure.
/// </summary>
public sealed class TurnEngine
{
    /// <summary>Turns of prose the narrator is reminded of. See <see cref="_historyTurns"/>.</summary>
    public const int DefaultHistoryTurns = 10;

    private readonly INarrator _narrator;
    private readonly IStateExtractor _extractor;
    private readonly IWorldRepository _repository;

    /// <summary>
    /// How many past turns of prose to replay to the narrator. A turn is a player input plus
    /// its narration, so the message count is double this.
    ///
    /// Zero disables the window, which is what the loop did before it existed — canon-only
    /// memory, and the narrator rewriting the scene from scratch every turn.
    /// </summary>
    private readonly int _historyTurns;

    /// <summary>
    /// The pack's lore. Authored content, not canon — it is read into prompts and never
    /// written, so it is held here rather than on <see cref="WorldState"/>.
    /// </summary>
    private readonly LoreBook _lore;

    /// <summary>Authored identity from the pack. Read into prompts, never written.</summary>
    private readonly IReadOnlyDictionary<string, CharacterSheet> _sheets;

    /// <summary>
    /// What the pack says its story is about. Empty when it says nothing, which is legal.
    ///
    /// Held beside the lore and sheets because it is the same kind of thing — authored content
    /// read into prompts and never written. It reaches the narrator only: see
    /// <see cref="IStateExtractor"/> for why the bookkeeping half is kept away from it.
    /// </summary>
    private readonly string _scenario;

    /// <summary>
    /// The pack's opening prose — the first thing the player read.
    ///
    /// <b>Held here rather than written to history, because it is content and history is what
    /// happened.</b> Storing it as a turn would bake today's text into every existing save, so
    /// editing the file between sessions would leave old prose in old worlds — the pack/save
    /// split, broken in the one place it is easiest to break it.
    ///
    /// It enters the narration window as the oldest beat while there is room, and falls out
    /// after <see cref="_historyTurns"/> real turns like any other prose. That lifetime is the
    /// whole difference between an opening and a scenario.
    /// </summary>
    private readonly string _opening;

    public TurnEngine(
        INarrator narrator,
        IStateExtractor extractor,
        IWorldRepository repository,
        int historyTurns = DefaultHistoryTurns,
        LoreBook? lore = null,
        IReadOnlyDictionary<string, CharacterSheet>? sheets = null,
        string scenario = "",
        string opening = "")
    {
        _narrator = narrator;
        _extractor = extractor;
        _repository = repository;
        _historyTurns = Math.Max(0, historyTurns);
        _lore = lore ?? LoreBook.Empty;
        _sheets = sheets ?? new Dictionary<string, CharacterSheet>(StringComparer.OrdinalIgnoreCase);
        _scenario = scenario ?? string.Empty;
        _opening = opening ?? string.Empty;
    }

    /// <summary>
    /// The scenario as the narrator should see it — <c>{{ }}</c> references resolved to names.
    ///
    /// Resolution happens here rather than at load because a reference renders to whatever the
    /// name is <i>now</i>: a character renamed on turn 40 must read correctly on turn 41. The
    /// pack loader checks that every reference points at something; this turns it into words.
    ///
    /// Skipping this was a real bug caught by eyeballing <c>/prose</c>: the narrator was handed
    /// a literal <c>{{player}}</c>, which is precisely the token-in-the-prose failure that the
    /// narration/extraction context split exists to prevent.
    /// </summary>
    private string Scenario(WorldState world) =>
        _scenario.Length == 0 ? _scenario : EntityReferences.Resolve(_scenario, world);

    public async Task<TurnOutcome> RunTurnAsync(
        string worldId,
        WorldState world,
        string playerInput,
        CancellationToken cancellationToken = default)
    {
        // Two renderings of the same state. The narrator gets names only — given an id it
        // will eventually write one into the prose — while the extractor cannot work without
        // them. Both are built before narration so the extractor sees the world as it was
        // when the prose was written, not as it is after this turn's changes.
        string narrationContext = ContextAssembler.ForNarration(world, _lore, _sheets);
        string extractionContext = ContextAssembler.ForExtraction(world, _lore, _sheets);

        IReadOnlyList<StoryBeat> recent = await LoadRecentAsync(worldId, cancellationToken)
            .ConfigureAwait(false);

        string narration = await _narrator
            .NarrateAsync(narrationContext, recent, playerInput, Scenario(world), cancellationToken)
            .ConfigureAwait(false);

        ExtractionResult extraction;
        string? extractionError = null;

        try
        {
            extraction = await _extractor
                .ExtractAsync(extractionContext, playerInput, narration, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Canon stands still for a turn; the story does not stop. Recorded so a run of
            // these is visible rather than showing up later as inexplicable drift.
            extraction = ExtractionResult.Empty();
            extractionError = ex.Message;
        }

        ValidationOutcome validation = DeltaValidator.Validate(world, extraction.Deltas, _lore);

        // Commit order: mutate, bump the turn counter, then persist. Nothing here can fail
        // partway in the in-memory case; the JSON repository is what makes the write atomic,
        // which is why that requirement lives on the interface.
        // The counter moves first, so a fact established this turn is stamped with the turn it
        // happened on. Applying first recorded everything one turn early: a fact accepted on
        // turn 7 carried establishedTurn 6, while LastSeenTurn — set after the increment —
        // was right, so the two disagreed about when "now" was.
        //
        // Found while auditing which turn produced a run of misfiled facts, and the off-by-one
        // made the trail read wrong.
        world.TurnNumber++;
        DeltaApplier.Apply(world, validation.Accepted);
        TouchPresentCharacters(world);

        TurnRecord record = new()
        {
            TurnNumber = world.TurnNumber,
            PlayerInput = playerInput,
            Narration = narration,
            Applied = validation.Accepted,
            NoOps = validation.NoOps,
            Rejected = validation.Rejected,
            RawExtraction = extraction.Raw,
            ExtractionProvider = extraction.Provider,
        };

        await _repository.SaveAsync(worldId, world, cancellationToken).ConfigureAwait(false);
        await _repository.AppendTurnAsync(worldId, record, cancellationToken).ConfigureAwait(false);

        return new TurnOutcome(record, extractionError);
    }

    /// <summary>
    /// Run extraction again over a turn that has already been narrated, using the stored
    /// player input and prose.
    ///
    /// For the case where the story was fine and only the bookkeeping failed — extraction
    /// timed out, or its output was rejected. Re-narrating would be wasteful and would
    /// *change the story the player already read*; everything needed is in the
    /// <see cref="TurnRecord"/>, so this is one cheap call against the small model.
    ///
    /// The world is passed as it stands now. Canon may have moved on since, which is exactly
    /// why validation runs again rather than the old deltas being trusted: a delta that was
    /// valid when first proposed can be a no-op or a conflict several turns later.
    ///
    /// Does not append a new turn. It repairs the record in place, because the story did not
    /// happen twice.
    /// </summary>
    public async Task<TurnOutcome> ReExtractAsync(
        string worldId,
        WorldState world,
        TurnRecord turn,
        CancellationToken cancellationToken = default)
    {
        string extractionContext = ContextAssembler.ForExtraction(world, _lore, _sheets);

        ExtractionResult extraction;
        string? extractionError = null;

        try
        {
            extraction = await _extractor
                .ExtractAsync(extractionContext, turn.PlayerInput, turn.Narration, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            extraction = ExtractionResult.Empty();
            extractionError = ex.Message;
        }

        ValidationOutcome validation = DeltaValidator.Validate(world, extraction.Deltas, _lore);
        DeltaApplier.Apply(world, validation.Accepted);

        // The turn number is not advanced and no record is appended: this is the same turn,
        // extracted again.
        TurnRecord repaired = turn with
        {
            Applied = validation.Accepted,
            NoOps = validation.NoOps,
            Rejected = validation.Rejected,
            RawExtraction = extraction.Raw,
            ExtractionProvider = extraction.Provider,
        };

        await _repository.SaveAsync(worldId, world, cancellationToken).ConfigureAwait(false);
        await _repository.ReplaceLastTurnAsync(worldId, repaired, cancellationToken).ConfigureAwait(false);

        return new TurnOutcome(repaired, extractionError);
    }

    /// <summary>
    /// Narrate the last turn again from the same player input, discarding the prose that was
    /// produced the first time.
    ///
    /// <b>Only when the turn changed nothing.</b> Deltas are not invertible —
    /// <c>MoodChanged(hald, "wary")</c> does not record what the mood was before — so there is
    /// no way to compute an undo from the turn log, and rerolling a turn that moved canon needs
    /// a snapshot taken before it was applied. That snapshot is designed and not built. A turn
    /// that applied nothing needs no undo at all, which makes this the free subset: roughly a
    /// quarter of turns in a real session, and <b>every</b> turn where narration failed
    /// outright, since prose that is not prose yields no deltas by definition.
    ///
    /// The discarded narration is never shown to the narrator. The window is rebuilt from
    /// history with the rerolled turn excluded, because a narrator handed the version being
    /// rejected will anchor on it and produce a paraphrase.
    ///
    /// Distinct from <see cref="ReExtractAsync"/>, and the difference is temperature. Narration
    /// runs hot, so this genuinely resamples and gives different prose. Extraction runs at
    /// zero, so re-extracting mostly reproduces the same deltas unless it failed outright.
    /// Neither command does the other's job.
    /// </summary>
    public async Task<RerollOutcome> RerollAsync(
        string worldId,
        WorldState world,
        TurnRecord turn,
        CancellationToken cancellationToken = default)
    {
        if (turn.Applied.Count > 0)
        {
            return RerollOutcome.Refused(
                $"That turn changed canon ({turn.Applied.Count} delta(s) applied), and deltas " +
                "cannot be undone without a snapshot of canon from before the turn. Rerolling " +
                "is only available on a turn that changed nothing.");
        }

        // Rebuilt with the rerolled turn dropped, so the narrator never sees the prose being
        // replaced.
        IReadOnlyList<StoryBeat> recent =
            await LoadRecentAsync(worldId, cancellationToken, skipLast: 1).ConfigureAwait(false);

        string narrationContext = ContextAssembler.ForNarration(world, _lore, _sheets);
        string extractionContext = ContextAssembler.ForExtraction(world, _lore, _sheets);

        string narration = await _narrator
            .NarrateAsync(narrationContext, recent, turn.PlayerInput, Scenario(world), cancellationToken)
            .ConfigureAwait(false);

        ExtractionResult extraction;
        string? extractionError = null;

        try
        {
            extraction = await _extractor
                .ExtractAsync(extractionContext, turn.PlayerInput, narration, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            extraction = ExtractionResult.Empty();
            extractionError = ex.Message;
        }

        ValidationOutcome validation = DeltaValidator.Validate(world, extraction.Deltas, _lore);
        DeltaApplier.Apply(world, validation.Accepted);

        // The turn number does not advance and no record is appended. This is the same turn,
        // told differently.
        TurnRecord replacement = turn with
        {
            Narration = narration,
            Applied = validation.Accepted,
            NoOps = validation.NoOps,
            Rejected = validation.Rejected,
            RawExtraction = extraction.Raw,
            ExtractionProvider = extraction.Provider,
        };

        await _repository.SaveAsync(worldId, world, cancellationToken).ConfigureAwait(false);
        await _repository.ReplaceLastTurnAsync(worldId, replacement, cancellationToken).ConfigureAwait(false);

        return RerollOutcome.Rerolled(new TurnOutcome(replacement, extractionError));
    }

    /// <summary>
    /// The last <see cref="_historyTurns"/> turns as prose, oldest first.
    ///
    /// Only narration gets this. Extraction deliberately does not: it scores 100% on the eval
    /// reading a single turn, and showing it earlier turns invites it to re-extract events
    /// that are already canon as though they were new.
    ///
    /// Reads the whole log to take the tail, which is O(n) per turn. Acceptable at the scale
    /// this phase plays at, and the repository interface already anticipates moving the turn
    /// log somewhere that can seek when it stops being acceptable.
    /// </summary>
    /// <param name="skipLast">
    /// Turns to drop from the end before taking the window. Used by
    /// <see cref="RerollAsync"/> so the narrator is not shown the prose it is replacing.
    /// </param>
    private async Task<IReadOnlyList<StoryBeat>> LoadRecentAsync(
        string worldId,
        CancellationToken cancellationToken,
        int skipLast = 0)
    {
        if (_historyTurns == 0)
        {
            return [];
        }

        IReadOnlyList<TurnRecord> history = await _repository
            .LoadHistoryAsync(worldId, cancellationToken)
            .ConfigureAwait(false);

        int available = Math.Max(0, history.Count - skipLast);

        List<StoryBeat> beats =
        [
            .. history
                .Take(available)
                .Skip(Math.Max(0, available - _historyTurns))
                .Select(t => new StoryBeat(t.PlayerInput, t.Narration)),
        ];

        // The opening is the oldest thing the narrator remembers, and only while the window has
        // not yet filled with real turns. An empty player input marks it as prose nobody
        // prompted: the narrator emits no user message for it.
        if (_opening.Length > 0 && beats.Count < _historyTurns)
        {
            beats.Insert(0, new StoryBeat(string.Empty, _opening));
        }

        return beats;
    }

    /// <summary>
    /// Mark everyone in the scene as seen this turn.
    ///
    /// Bookkeeping the extractor should not be asked to do. Presence is derivable from state
    /// we already hold, and every question delegated to the model is another thing it can get
    /// wrong — the probe made clear that its budget for judgement is better spent elsewhere.
    /// </summary>
    private static void TouchPresentCharacters(WorldState world)
    {
        foreach (Character npc in world.NpcsWithPlayer())
        {
            npc.LastSeenTurn = world.TurnNumber;
        }
    }
}

/// <summary>
/// The result of a turn. <paramref name="ExtractionError"/> is non-null when extraction
/// failed outright, as distinct from succeeding and having its output rejected — those are
/// different problems and the harness should not present them as the same one.
/// </summary>
public sealed record TurnOutcome(TurnRecord Turn, string? ExtractionError)
{
    public bool ExtractionFailed => ExtractionError is not null;
}

/// <summary>
/// The result of a reroll attempt, which has an outcome the ordinary turn does not: it can be
/// legitimately <b>refused</b>.
///
/// A refusal is not an error. It means the turn moved canon and the undo needed to take it back
/// does not exist yet, which is a limit worth stating to the player in those terms rather than
/// failing in a way that reads like a bug.
/// </summary>
public sealed record RerollOutcome(TurnOutcome? Outcome, string? RefusedBecause)
{
    public static RerollOutcome Rerolled(TurnOutcome outcome) => new(outcome, null);

    public static RerollOutcome Refused(string reason) => new(null, reason);

    public bool WasRefused => RefusedBecause is not null;
}
