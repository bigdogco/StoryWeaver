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

    public TurnEngine(
        INarrator narrator,
        IStateExtractor extractor,
        IWorldRepository repository,
        int historyTurns = DefaultHistoryTurns)
    {
        _narrator = narrator;
        _extractor = extractor;
        _repository = repository;
        _historyTurns = Math.Max(0, historyTurns);
    }

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
        string narrationContext = ContextAssembler.ForNarration(world);
        string extractionContext = ContextAssembler.ForExtraction(world);

        IReadOnlyList<StoryBeat> recent = await LoadRecentAsync(worldId, cancellationToken)
            .ConfigureAwait(false);

        string narration = await _narrator
            .NarrateAsync(narrationContext, recent, playerInput, cancellationToken)
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

        ValidationOutcome validation = DeltaValidator.Validate(world, extraction.Deltas);

        // Commit order: mutate, bump the turn counter, then persist. Nothing here can fail
        // partway in the in-memory case; the JSON repository is what makes the write atomic,
        // which is why that requirement lives on the interface.
        DeltaApplier.Apply(world, validation.Accepted);
        world.TurnNumber++;
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
        };

        await _repository.SaveAsync(worldId, world, cancellationToken).ConfigureAwait(false);
        await _repository.AppendTurnAsync(worldId, record, cancellationToken).ConfigureAwait(false);

        return new TurnOutcome(record, extractionError);
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
    private async Task<IReadOnlyList<StoryBeat>> LoadRecentAsync(
        string worldId,
        CancellationToken cancellationToken)
    {
        if (_historyTurns == 0)
        {
            return [];
        }

        IReadOnlyList<TurnRecord> history = await _repository
            .LoadHistoryAsync(worldId, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. history
                .Skip(Math.Max(0, history.Count - _historyTurns))
                .Select(t => new StoryBeat(t.PlayerInput, t.Narration)),
        ];
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
