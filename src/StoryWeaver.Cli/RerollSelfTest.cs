using StoryWeaver.Core;

namespace StoryWeaver.Cli;

/// <summary>
/// Offline checks on reroll and on the rules protecting a character's identity, using fake
/// ports so no model is called.
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

        failures += CheckStoryCannotRenameThePlayer();
        failures += CheckPlayerCanRenameThemselves();
        failures += CheckNameCannotBeTheId();
        failures += CheckRefusesWhenCanonMoved();
        failures += CheckDiscardedProseIsHidden();
        failures += CheckReplacesRatherThanAppends();

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "All reroll and identity checks passed."
            : $"{failures} reroll/identity check(s) FAILED.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// The bug this guards, from live play: turn 38 emitted <c>character_renamed</c> on the
    /// player, replacing the name "You" with the literal id string and wiping "A traveller,
    /// recently arrived in Marrow" with a passing injury. Both halves were destructive and
    /// neither was recoverable.
    /// </summary>
    private static int CheckStoryCannotRenameThePlayer()
    {
        WorldState world = WorldSeeds.Marrow();

        ValidationOutcome outcome = DeltaValidator.Validate(
            world,
            [new CharacterRenamed(Character.PlayerId, "player", "burned, with a blistered hand")]);

        if (outcome.Rejected.Count != 1 || outcome.Accepted.Count != 0)
        {
            Console.WriteLine("  FAIL  the story was allowed to rename the player.");
            return 1;
        }

        Console.WriteLine("  ok    the story cannot rename the player");
        return 0;
    }

    /// <summary>
    /// The other half. The rule protects the player from the *story*, and must not stop the
    /// player describing their own character — which is the thing it exists to protect.
    /// </summary>
    private static int CheckPlayerCanRenameThemselves()
    {
        WorldState world = WorldSeeds.Marrow();

        ValidationOutcome outcome = DeltaValidator.Validate(
            world,
            [new CharacterRenamed(Character.PlayerId, "Pavel", "A King's Investigator, travel-stained.")],
            lore: null,
            authored: true);

        if (outcome.Accepted.Count != 1)
        {
            Console.WriteLine("  FAIL  the player could not rename their own character.");
            return 1;
        }

        DeltaApplier.Apply(world, outcome.Accepted);

        if (world.Player?.Name != "Pavel")
        {
            Console.WriteLine("  FAIL  an authored player rename did not apply.");
            return 1;
        }

        Console.WriteLine("  ok    the player can rename their own character");
        return 0;
    }

    /// <summary>
    /// A name equal to the id is the model echoing the key back rather than writing a name. It
    /// reads as a rename that worked and leaves "innkeeper-hald" in the prose.
    /// </summary>
    private static int CheckNameCannotBeTheId()
    {
        WorldState world = WorldSeeds.Marrow();

        ValidationOutcome outcome = DeltaValidator.Validate(
            world,
            [new CharacterRenamed("innkeeper-hald", "innkeeper-hald", null)]);

        if (outcome.Rejected.Count != 1)
        {
            Console.WriteLine("  FAIL  a character was renamed to its own id.");
            return 1;
        }

        Console.WriteLine("  ok    a character cannot be renamed to its own id");
        return 0;
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
