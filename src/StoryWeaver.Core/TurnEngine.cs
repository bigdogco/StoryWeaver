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
    private readonly INarrator _narrator;
    private readonly IStateExtractor _extractor;
    private readonly IWorldRepository _repository;

    public TurnEngine(INarrator narrator, IStateExtractor extractor, IWorldRepository repository)
    {
        _narrator = narrator;
        _extractor = extractor;
        _repository = repository;
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

        string narration = await _narrator
            .NarrateAsync(narrationContext, playerInput, cancellationToken)
            .ConfigureAwait(false);

        ExtractionResult extraction;
        string? extractionError = null;

        try
        {
            extraction = await _extractor
                .ExtractAsync(extractionContext, narration, cancellationToken)
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
