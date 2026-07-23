using StoryWeaver.Core;
using StoryWeaver.Llm.Configuration;
using StoryWeaver.Llm.Logging;
using StoryWeaver.Llm.OpenRouter;
using StoryWeaver.Llm.Story;
using StoryWeaver.Storage;

namespace StoryWeaver.Cli;

/// <summary>
/// Playable console harness. Throwaway, but now persistent — the world is saved to disk after
/// every turn, so a session can be quit and resumed.
///
/// The point of this is not the game. It is watching what extraction does over many turns:
/// the rejection list is printed after every turn precisely because a silently dropped
/// delta is the failure mode that would otherwise take fifty turns to notice.
/// </summary>
internal static class PlaySession
{
    private const string WorldId = "marrow";
    private const string SaveRoot = "saves";

    public static async Task<int> RunAsync(StoryWeaverSettings settings)
    {
        FileLlmLog log = new(settings.Logging);
        using OpenRouterClient client = new(settings, log);

        JsonWorldRepository repository = new(SaveRoot);
        int historyTurns = settings.Story.HistoryTurns;
        TurnEngine engine = new(
            new LlmNarrator(client),
            new LlmStateExtractor(client),
            repository,
            historyTurns);

        // Resume the world if it exists, otherwise seed it and write the first save so the
        // world is on disk before any turn runs.
        WorldState? loaded = await repository.LoadAsync(WorldId).ConfigureAwait(false);
        bool resumed = loaded is not null;
        WorldState world = loaded ?? WorldSeeds.Marrow();

        if (!resumed)
        {
            await repository.SaveAsync(WorldId, world).ConfigureAwait(false);
        }

        PrintBanner(log.FilePath, repository.RootDirectory, resumed, world.TurnNumber, historyTurns);

        if (resumed)
        {
            // The narrator gets the same window as prose; the player should not be the only
            // one in the room with no memory of what just happened. This replaces the opening
            // scene rather than following it — the story tail already establishes where they
            // are, and a static room description after it reads as a reset.
            await PrintRecentAsync(repository, historyTurns).ConfigureAwait(false);
        }
        else
        {
            PrintOpeningScene(world);
        }

        while (true)
        {
            Console.Write("\n> ");
            string? input = Console.ReadLine();

            if (input is null || input.Trim() is "/quit" or "/q")
            {
                Console.WriteLine($"\nEnding session. World saved under {repository.RootDirectory}.");
                return 0;
            }

            input = input.Trim();

            if (input.Length == 0)
            {
                continue;
            }

            if (input.StartsWith('/'))
            {
                if (string.Equals(input, "/retry", StringComparison.OrdinalIgnoreCase))
                {
                    await RetryExtractionAsync(engine, repository, world).ConfigureAwait(false);
                }
                else if (!await AuthoringCommands
                        .TryHandleAsync(input, WorldId, world, repository)
                        .ConfigureAwait(false))
                {
                    HandleCommand(input, world, repository);
                }

                continue;
            }

            try
            {
                Console.WriteLine("\n(thinking...)\n");
                TurnOutcome outcome = await engine
                    .RunTurnAsync(WorldId, world, input)
                    .ConfigureAwait(false);

                PrintTurn(outcome);
            }
            catch (StoryWeaverException ex)
            {
                Console.WriteLine($"Turn failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Re-run extraction over the last turn's stored prose.
    ///
    /// For when the story was fine and only the bookkeeping failed — a timed-out extraction
    /// leaves the narration on screen and in history while canon stands still, and a run of
    /// those is exactly the drift this architecture exists to prevent. Re-narrating instead
    /// would change prose the player has already read.
    /// </summary>
    private static async Task RetryExtractionAsync(
        TurnEngine engine,
        IWorldRepository repository,
        WorldState world)
    {
        IReadOnlyList<TurnRecord> history =
            await repository.LoadHistoryAsync(WorldId).ConfigureAwait(false);

        if (history.Count == 0)
        {
            Console.WriteLine("No turns to retry yet.");
            return;
        }

        Console.WriteLine("\n(re-extracting the last turn...)\n");

        try
        {
            TurnOutcome outcome = await engine
                .ReExtractAsync(WorldId, world, history[^1])
                .ConfigureAwait(false);

            if (outcome.ExtractionFailed)
            {
                Console.WriteLine($"  [!] Extraction failed again: {outcome.ExtractionError}");
                return;
            }

            PrintDeltas(outcome.Turn);
        }
        catch (StoryWeaverException ex)
        {
            Console.WriteLine($"Retry failed: {ex.Message}");
        }
    }

    private static void PrintTurn(TurnOutcome outcome)
    {
        Console.WriteLine(outcome.Turn.Narration);
        Console.WriteLine();

        if (outcome.ExtractionFailed)
        {
            // Distinct from "extraction ran and produced nothing usable". The story continued
            // but canon did not move, and a run of these means drift.
            Console.WriteLine($"  [!] Extraction failed: {outcome.ExtractionError}");
            Console.WriteLine("  [!] The story continued but canon did not. Use /retry to");
            Console.WriteLine("  [!] extract this turn again without rewriting the prose.");
            return;
        }

        PrintDeltas(outcome.Turn);
    }

    private static void PrintDeltas(TurnRecord turn)
    {
        Console.WriteLine($"  --- turn {turn.TurnNumber} ---");

        if (turn.Applied.Count == 0)
        {
            Console.WriteLine("  applied: nothing");
        }
        else
        {
            foreach (StateDelta delta in turn.Applied)
            {
                Console.WriteLine($"  applied:  {Describe(delta)}");
            }
        }

        foreach (StateDelta delta in turn.NoOps)
        {
            Console.WriteLine($"  no-op:    {Describe(delta)} (already true)");
        }

        foreach (RejectedDelta rejected in turn.Rejected)
        {
            Console.WriteLine($"  REJECTED: {Describe(rejected.Delta)}");
            Console.WriteLine($"            {rejected.Reason}");
        }
    }

    private static string Describe(StateDelta delta) => delta switch
    {
        CharacterMoved d => $"{d.CharacterId} -> {d.ToLocationId}",
        PlayerMoved d => $"player -> {d.ToLocationId}",
        StatusChanged d => $"{d.CharacterId} status = {d.Status}",
        MoodChanged d => $"{d.CharacterId} mood = {d.Mood}",
        RelationshipChanged d => $"{d.CharacterId} standing = {d.Standing} ({d.Summary})",
        FactEstablished d => $"fact {d.FactId}: {d.Text}",
        FactLearned d => $"{d.CharacterId} learned {d.FactId}",
        CharacterIntroduced d => $"new character {d.CharacterId} ({d.Name})",
        CharacterRenamed d => $"{d.CharacterId} is now called {d.Name}",
        LocationIntroduced d => $"new location {d.LocationId} ({d.Name})",
        _ => delta.GetType().Name,
    };

    private static void HandleCommand(string input, WorldState world, IWorldRepository repo)
    {
        switch (input)
        {
            case "/state":
                Console.WriteLine();
                Console.WriteLine(ContextAssembler.ForExtraction(world));
                break;

            // Worth having its own command: this is the view that must contain no ids, and
            // eyeballing it is the only check that the narrator cannot leak one into prose.
            case "/prose":
                Console.WriteLine();
                Console.WriteLine(ContextAssembler.ForNarration(world));
                break;

            case "/raw":
                PrintLastRaw(repo);
                break;

            case "/help":
                Console.WriteLine("  Write *actions between asterisks* and speech outside them.");
                Console.WriteLine("  Both are authoritative — the narrator will not rewrite them.");
                Console.WriteLine();
                Console.WriteLine("  /state  world state as the extractor sees it (with ids)");
                Console.WriteLine("  /prose  world state as the narrator sees it (no ids)");
                Console.WriteLine("  /raw    last raw extraction response");
                Console.WriteLine("  /retry  extract the last turn again, same prose");
                Console.WriteLine("  /quit   end the session");
                Console.WriteLine();
                Console.WriteLine("  Write to canon yourself — extraction only records what is");
                Console.WriteLine("  present in a scene, never what is merely mentioned:");
                Console.WriteLine();
                Console.WriteLine("  /place      add a location");
                Console.WriteLine("  /character  add a person (may be offstage)");
                Console.WriteLine("  /fact       add a truth, and choose whether you know it");
                Console.WriteLine("  /rename     rename someone — their id stays the same");
                break;

            default:
                Console.WriteLine($"Unknown command '{input}'. Try /help.");
                break;
        }
    }

    private static void PrintLastRaw(IWorldRepository repo)
    {
        IReadOnlyList<TurnRecord> history = repo.LoadHistoryAsync(WorldId).GetAwaiter().GetResult();

        if (history.Count == 0)
        {
            Console.WriteLine("No turns yet.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine(history[^1].RawExtraction ?? "(no raw extraction recorded)");
    }

    /// <summary>
    /// Replay the tail of the story so a resumed session reads as a continuation. Shows the
    /// same turns the narrator is being reminded of, so what the player sees and what the
    /// model remembers do not quietly diverge.
    /// </summary>
    private static async Task PrintRecentAsync(IWorldRepository repo, int historyTurns)
    {
        if (historyTurns <= 0)
        {
            return;
        }

        IReadOnlyList<TurnRecord> history = await repo.LoadHistoryAsync(WorldId).ConfigureAwait(false);

        if (history.Count == 0)
        {
            return;
        }

        Console.WriteLine($"--- the story so far (last {Math.Min(historyTurns, history.Count)} turns) ---");
        Console.WriteLine();

        foreach (TurnRecord turn in history.Skip(Math.Max(0, history.Count - historyTurns)))
        {
            Console.WriteLine($"> {turn.PlayerInput}");
            Console.WriteLine();
            Console.WriteLine(turn.Narration);
            Console.WriteLine();
        }
    }

    private static void PrintBanner(
        string logPath,
        string saveRoot,
        bool resumed,
        int turnNumber,
        int historyTurns)
    {
        Console.WriteLine();
        Console.WriteLine(resumed
            ? $"StoryWeaver — play session (resumed at turn {turnNumber})"
            : "StoryWeaver — play session (new world)");
        Console.WriteLine($"Saving to  {saveRoot}");
        Console.WriteLine($"Logging to {logPath}");
        Console.WriteLine($"Narrator remembers the last {historyTurns} turns");
        Console.WriteLine();
        Console.WriteLine("Write *actions between asterisks* and speech outside them:");
        Console.WriteLine();
        Console.WriteLine("  *I lean on the counter.* What do you know about the well?");
        Console.WriteLine();
        Console.WriteLine("Plain instructions work too. Commands: /state /prose /raw /help /quit");
        Console.WriteLine();
    }

    private static void PrintOpeningScene(WorldState world)
    {
        // Wherever the player currently stands — the tavern in a fresh world, but possibly
        // elsewhere in a resumed one.
        Location? here = world.PlayerLocationId is { } id ? world.FindLocation(id) : null;
        here ??= world.Locations.GetValueOrDefault("marrow-tavern");

        if (here is null)
        {
            return;
        }

        Console.WriteLine(new string('-', 70));
        Console.WriteLine(here.Description);
        Console.WriteLine(new string('-', 70));
    }

}
