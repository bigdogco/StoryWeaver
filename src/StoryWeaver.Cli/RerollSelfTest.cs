using StoryWeaver.Core;

namespace StoryWeaver.Cli;

/// <summary>
/// Offline checks on reroll, using fake ports so no model is called.
///
/// Two properties are worth holding. **A turn that moved canon must be refused**, because the
/// undo it would need does not exist — deltas carry no previous value, so applying them is
/// one-way until a canon snapshot is built. And **the discarded prose must not reach the
/// narrator**, or it anchors on the version being rejected and returns a paraphrase of the
/// thing the player just threw away.
///
/// The second is the kind of bug that never announces itself: the reroll works, the prose is
/// different enough to look resampled, and the feature is quietly worth much less than it
/// appears.
/// </summary>
internal static class RerollSelfTest
{
    public static int Run()
    {
        int failures = 0;

        failures += CheckRefusesWhenCanonMoved();
        failures += CheckDiscardedProseIsHidden();
        failures += CheckReplacesRatherThanAppends();

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "All reroll checks passed."
            : $"{failures} reroll check(s) FAILED.");

        return failures == 0 ? 0 : 1;
    }

    private static int CheckRefusesWhenCanonMoved()
    {
        FakeRepository repo = new();
        repo.Turns.Add(Turn(1, "look around", "You look around.",
            applied: [new MoodChanged("innkeeper-hald", "wary")]));

        TurnEngine engine = new(new FakeNarrator(), new FakeExtractor(), repo);

        RerollOutcome outcome = engine
            .RerollAsync("w", WorldSeeds.Marrow(), repo.Turns[^1])
            .GetAwaiter().GetResult();

        if (!outcome.WasRefused)
        {
            Console.WriteLine("  FAIL  a turn that applied deltas was rerolled anyway.");
            return 1;
        }

        Console.WriteLine("  ok    a turn that moved canon is refused");
        return 0;
    }

    private static int CheckDiscardedProseIsHidden()
    {
        FakeRepository repo = new();
        repo.Turns.Add(Turn(1, "first", "FIRST NARRATION"));
        repo.Turns.Add(Turn(2, "second", "DISCARD ME"));

        FakeNarrator narrator = new();
        TurnEngine engine = new(narrator, new FakeExtractor(), repo);

        engine.RerollAsync("w", WorldSeeds.Marrow(), repo.Turns[^1]).GetAwaiter().GetResult();

        if (narrator.SawNarration.Contains("DISCARD ME"))
        {
            Console.WriteLine("  FAIL  the narrator was shown the prose being replaced.");
            return 1;
        }

        if (!narrator.SawNarration.Contains("FIRST NARRATION"))
        {
            Console.WriteLine("  FAIL  the narrator lost the earlier history it should still see.");
            return 1;
        }

        Console.WriteLine("  ok    the discarded prose is hidden, earlier history is kept");
        return 0;
    }

    private static int CheckReplacesRatherThanAppends()
    {
        FakeRepository repo = new();
        repo.Turns.Add(Turn(1, "look", "old prose"));

        TurnEngine engine = new(new FakeNarrator(), new FakeExtractor(), repo);
        WorldState world = WorldSeeds.Marrow();
        int before = world.TurnNumber;

        engine.RerollAsync("w", world, repo.Turns[^1]).GetAwaiter().GetResult();

        if (repo.Turns.Count != 1)
        {
            Console.WriteLine($"  FAIL  reroll left {repo.Turns.Count} turns in history, expected 1.");
            return 1;
        }

        if (repo.Turns[0].Narration == "old prose")
        {
            Console.WriteLine("  FAIL  reroll did not replace the narration.");
            return 1;
        }

        if (world.TurnNumber != before)
        {
            Console.WriteLine("  FAIL  reroll advanced the turn counter.");
            return 1;
        }

        Console.WriteLine("  ok    reroll replaces the turn without advancing the counter");
        return 0;
    }

    private static TurnRecord Turn(
        int number,
        string input,
        string narration,
        IReadOnlyList<StateDelta>? applied = null) => new()
    {
        TurnNumber = number,
        PlayerInput = input,
        Narration = narration,
        Applied = applied ?? [],
        NoOps = [],
        Rejected = [],
        RawExtraction = "{}",
    };

    private sealed class FakeNarrator : INarrator
    {
        /// <summary>Every piece of prose the narrator was shown as history.</summary>
        public List<string> SawNarration { get; } = [];

        public Task<string> NarrateAsync(
            string context,
            IReadOnlyList<StoryBeat> recent,
            string playerInput,
            CancellationToken cancellationToken = default)
        {
            SawNarration.AddRange(recent.Select(b => b.Narration));
            return Task.FromResult("rerolled prose");
        }
    }

    private sealed class FakeExtractor : IStateExtractor
    {
        public Task<ExtractionResult> ExtractAsync(
            string context,
            string playerInput,
            string narration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ExtractionResult([], "{\"deltas\":[]}", null, null));
    }

    private sealed class FakeRepository : IWorldRepository
    {
        public List<TurnRecord> Turns { get; } = [];

        public Task<WorldState?> LoadAsync(string worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult<WorldState?>(null);

        public Task SaveAsync(string worldId, WorldState state, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AppendTurnAsync(string worldId, TurnRecord turn, CancellationToken cancellationToken = default)
        {
            Turns.Add(turn);
            return Task.CompletedTask;
        }

        public Task ReplaceLastTurnAsync(string worldId, TurnRecord turn, CancellationToken cancellationToken = default)
        {
            Turns[^1] = turn;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TurnRecord>> LoadHistoryAsync(
            string worldId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TurnRecord>>(Turns);

        public Task<IReadOnlyList<string>> ListWorldsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }
}
