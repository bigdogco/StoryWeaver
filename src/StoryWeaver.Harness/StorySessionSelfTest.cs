using StoryWeaver.Core;

namespace StoryWeaver.Harness;

/// <summary>
/// Offline checks on <see cref="StorySession"/> — the object that owns canon.
///
/// The point of these is the guard, and the guard protects against something no console can
/// produce: a second operation starting while a turn sits in the twenty-to-sixty seconds of
/// network between reading canon and writing it. So the narrator here is a fake that blocks
/// until released, which is the only way to hold a turn open on purpose and let a second
/// operation try to run underneath it.
/// </summary>
internal static class StorySessionSelfTest
{
    public static int Run()
    {
        Console.WriteLine("StorySession self-test");

        int failures = 0;

        failures += CheckSecondOperationIsRefused();
        failures += CheckGuardSurvivesFailure();
        failures += CheckTheDesignsRace();
        failures += CheckEditPersistsAndReports();
        failures += CheckEditIsNeverRefusedForBeingWrong();
        failures += CheckAuthorAcceptingNothingWritesNothing();
        failures += CheckNoTurnsRefusals();
        failures += CheckDisposeReleasesTheLock();

        Console.WriteLine(failures == 0
            ? "  all StorySession checks passed"
            : $"  {failures} StorySession check(s) failed");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// The guard itself. A turn is held open, a second operation is attempted, and it must come
    /// back refused rather than running against canon somebody else is mid-way through.
    /// </summary>
    private static int CheckSecondOperationIsRefused()
    {
        BlockingNarrator narrator = new();
        using StorySession session = Session(narrator, out _);

        Task<SessionResult<TurnOutcome>> turn = session.TakeTurnAsync("I look around.");
        narrator.WaitUntilCalled();

        if (!session.IsBusy)
        {
            Console.WriteLine("  FAIL  a session with a turn in flight does not report busy.");
            narrator.Release();
            return 1;
        }

        SessionResult<RefreshReport> update = session.UpdateStateAsync().GetAwaiter().GetResult();
        SessionResult<ValidationOutcome> author = session
            .AuthorAsync(Authoring.Place("a-cellar", "A cellar", "Damp.")).GetAwaiter().GetResult();

        narrator.Release();
        turn.GetAwaiter().GetResult();

        if (!update.WasRefused || !author.WasRefused)
        {
            Console.WriteLine(
                $"  FAIL  expected both refused; update={update.WasRefused}, author={author.WasRefused}.");
            return 1;
        }

        if (session.IsBusy)
        {
            Console.WriteLine("  FAIL  the session is still busy after the turn finished.");
            return 1;
        }

        Console.WriteLine("  ok    a second operation is refused while a turn is in flight");
        return 0;
    }

    /// <summary>
    /// A thrown turn must release the guard. Otherwise one network failure wedges the session
    /// shut for the rest of the run, and every later operation refuses forever.
    /// </summary>
    private static int CheckGuardSurvivesFailure()
    {
        using StorySession session = Session(new ThrowingNarrator(), out _);

        try
        {
            session.TakeTurnAsync("I look around.").GetAwaiter().GetResult();
            Console.WriteLine("  FAIL  the throwing narrator did not throw.");
            return 1;
        }
        catch (InvalidOperationException)
        {
        }

        if (session.IsBusy)
        {
            Console.WriteLine("  FAIL  the guard was not released after a failure.");
            return 1;
        }

        Console.WriteLine("  ok    a failed operation releases the guard");
        return 0;
    }

    /// <summary>
    /// **The race `design/CANON_OWNERSHIP.md` describes**, and the reason this class exists.
    ///
    /// Before the session owned canon, Update State swapped a reference the in-flight turn was
    /// not holding: the turn then mutated the old graph and saved it, discarding both the
    /// reload and the edit that prompted it. Here the update is attempted mid-turn, and the
    /// only two acceptable outcomes are that it is refused, or that it happens without losing
    /// the turn — never that the turn's own work disappears.
    ///
    /// <b>What it also pins is the limit.</b> The guard stops canon being half-updated; it does
    /// not preserve an external edit made <i>while</i> a turn was running, because the turn
    /// saves the session's canon over the file at the end. That is the same consequence as
    /// editing without <c>/reload</c> at all, and it is asserted here so it stays a known trade
    /// rather than a surprise.
    /// </summary>
    private static int CheckTheDesignsRace()
    {
        BlockingNarrator narrator = new();
        using StorySession session = Session(narrator, out InMemoryWorldRepository repository);

        // Something is edited on disk while the session is not looking, exactly as a person
        // with a text editor would.
        WorldState onDisk = Sample();
        onDisk.Locations["hand-edited"] = new Location { Id = "hand-edited", Name = "Edited" };
        repository.SaveAsync("test", onDisk).GetAwaiter().GetResult();

        Task<SessionResult<TurnOutcome>> turn = session.TakeTurnAsync("I look around.");
        narrator.WaitUntilCalled();

        SessionResult<RefreshReport> update = session.UpdateStateAsync().GetAwaiter().GetResult();

        narrator.Release();
        SessionResult<TurnOutcome> completed = turn.GetAwaiter().GetResult();

        if (!update.WasRefused)
        {
            Console.WriteLine("  FAIL  an update landed in the middle of a turn.");
            return 1;
        }

        if (completed.WasRefused || completed.Value!.Turn.TurnNumber != 1)
        {
            Console.WriteLine("  FAIL  the turn did not complete normally.");
            return 1;
        }

        // And the limit, pinned rather than left to be discovered later. The turn saves the
        // session's canon at the end, so it overwrites the file the edit was made in — the
        // edit is gone, and a later update cannot find it. Refusing protects canon from being
        // half-updated; it does not preserve an edit made while a turn was running, which is
        // the same consequence as editing without /reload at all.
        SessionResult<RefreshReport> after = session.UpdateStateAsync().GetAwaiter().GetResult();

        if (after.WasRefused)
        {
            Console.WriteLine("  FAIL  an update after the turn finished was refused.");
            return 1;
        }

        if (session.World.Locations.ContainsKey("hand-edited"))
        {
            Console.WriteLine("  FAIL  the edit survived — this test's premise is now wrong, re-read it.");
            return 1;
        }

        Console.WriteLine("  ok    an update cannot land mid-turn (and the turn still wins the file)");
        return 0;
    }

    private static int CheckEditPersistsAndReports()
    {
        using StorySession session = Session(new BlockingNarrator(), out InMemoryWorldRepository repository);

        SessionResult<EditReport> result = session
            .EditAsync(w => w.Locations["marrow-tavern"].Description = "Rewritten by hand.")
            .GetAwaiter().GetResult();

        if (result.WasRefused || !result.Value!.IsClean)
        {
            Console.WriteLine("  FAIL  a harmless edit was refused or reported warnings.");
            return 1;
        }

        WorldState? saved = repository.LoadAsync("test").GetAwaiter().GetResult();

        if (saved?.Locations["marrow-tavern"].Description != "Rewritten by hand.")
        {
            Console.WriteLine("  FAIL  the edit was not persisted.");
            return 1;
        }

        Console.WriteLine("  ok    a direct edit is applied, saved, and reported clean");
        return 0;
    }

    /// <summary>
    /// The hatch reports and never refuses. Canon belongs to the player; being argued with is
    /// the validator's posture toward a cheap model, not toward a person editing their world.
    /// </summary>
    private static int CheckEditIsNeverRefusedForBeingWrong()
    {
        using StorySession session = Session(new BlockingNarrator(), out InMemoryWorldRepository repository);

        SessionResult<EditReport> result = session
            .EditAsync(w => w.Characters["innkeeper-hald"].LocationId = "nowhere-at-all")
            .GetAwaiter().GetResult();

        if (result.WasRefused)
        {
            Console.WriteLine("  FAIL  an edit that breaks an invariant was refused.");
            return 1;
        }

        if (result.Value!.IsClean)
        {
            Console.WriteLine("  FAIL  an edit that breaks an invariant reported no warnings.");
            return 1;
        }

        WorldState? saved = repository.LoadAsync("test").GetAwaiter().GetResult();

        if (saved?.Characters["innkeeper-hald"].LocationId != "nowhere-at-all")
        {
            Console.WriteLine("  FAIL  the edit was reported but not saved.");
            return 1;
        }

        Console.WriteLine("  ok    a damaging edit is saved and reported, never refused");
        return 0;
    }

    private static int CheckAuthorAcceptingNothingWritesNothing()
    {
        using StorySession session = Session(new BlockingNarrator(), out InMemoryWorldRepository repository);

        // Reusing a character id as a location — rejected, as JsonSelfTest establishes.
        SessionResult<ValidationOutcome> result = session
            .AuthorAsync(Authoring.Place("innkeeper-hald", "The Crow", "A taproom."))
            .GetAwaiter().GetResult();

        if (result.WasRefused || result.Value!.Accepted.Count != 0)
        {
            Console.WriteLine("  FAIL  expected a clean refusal of the delta, not of the call.");
            return 1;
        }

        if (session.World.Locations.ContainsKey("innkeeper-hald"))
        {
            Console.WriteLine("  FAIL  a rejected authored delta changed canon.");
            return 1;
        }

        Console.WriteLine("  ok    authoring nothing acceptable changes nothing");
        return 0;
    }

    /// <summary>
    /// "There are no turns yet" used to be a caller's job — both clients loaded history to find
    /// out. Now it is a refusal like any other, so a caller has one kind of no to handle.
    /// </summary>
    private static int CheckNoTurnsRefusals()
    {
        using StorySession session = Session(new BlockingNarrator(), out _);

        SessionResult<TurnOutcome> retry = session.ReExtractLastAsync().GetAwaiter().GetResult();
        SessionResult<TurnOutcome> reroll = session.RerollLastAsync().GetAwaiter().GetResult();

        if (!retry.WasRefused || !reroll.WasRefused)
        {
            Console.WriteLine("  FAIL  re-extract or reroll on an empty history did not refuse.");
            return 1;
        }

        Console.WriteLine("  ok    an empty history refuses rather than throwing");
        return 0;
    }

    private static int CheckDisposeReleasesTheLock()
    {
        DisposeSpy spy = new();

        StorySession session = Session(new BlockingNarrator(), out _, spy);
        session.Dispose();

        if (!spy.Disposed)
        {
            Console.WriteLine("  FAIL  disposing the session did not release the save lock.");
            return 1;
        }

        // Disposing twice must not throw — the console keeps its own `using` on the lock so a
        // failure between acquiring it and building the session cannot leak it.
        session.Dispose();

        Console.WriteLine("  ok    disposing the session releases the lock, and twice is safe");
        return 0;
    }

    private static StorySession Session(
        INarrator narrator,
        out InMemoryWorldRepository repository,
        IDisposable? owned = null)
    {
        repository = new InMemoryWorldRepository();
        WorldState world = Sample();
        repository.SaveAsync("test", world).GetAwaiter().GetResult();

        TurnEngine engine = new(narrator, new NoOpExtractor(), repository);

        return new StorySession(
            "test", "marrow", world, engine, repository, LoreBook.Empty,
            owned is null ? null : [owned]);
    }

    private static WorldState Sample()
    {
        WorldState world = new();
        world.Locations["marrow-tavern"] = new Location { Id = "marrow-tavern", Name = "The Drowned Crow" };
        world.Characters["player"] = new Character { Id = "player", Name = "Pavel", LocationId = "marrow-tavern" };
        world.Characters["innkeeper-hald"] = new Character { Id = "innkeeper-hald", Name = "Hald", LocationId = "marrow-tavern" };
        return world;
    }

    /// <summary>
    /// Holds a turn open on demand. This is the whole reason the guard is testable: a real
    /// narrator's twenty-to-sixty-second window cannot be entered deliberately, and a fake that
    /// returns instantly never overlaps with anything.
    /// </summary>
    private sealed class BlockingNarrator : INarrator
    {
        private readonly SemaphoreSlim _called = new(0, 1);
        private readonly SemaphoreSlim _release = new(0, 1);
        private bool _blocking = true;

        public void WaitUntilCalled() => _called.Wait(TimeSpan.FromSeconds(5));

        public void Release()
        {
            _blocking = false;
            _release.Release();
        }

        public async Task<string> NarrateAsync(
            string context,
            IReadOnlyList<StoryBeat> recent,
            string playerInput,
            string scenario = "",
            CancellationToken cancellationToken = default)
        {
            if (_blocking)
            {
                _called.Release();
                await _release.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }

            return "The taproom is quiet.";
        }
    }

    private sealed class ThrowingNarrator : INarrator
    {
        public Task<string> NarrateAsync(
            string context,
            IReadOnlyList<StoryBeat> recent,
            string playerInput,
            string scenario = "",
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("narration failed on purpose");
    }

    private sealed class NoOpExtractor : IStateExtractor
    {
        public Task<ExtractionResult> ExtractAsync(
            string context,
            string playerInput,
            string narration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ExtractionResult.Empty());
    }

    private sealed class DisposeSpy : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
