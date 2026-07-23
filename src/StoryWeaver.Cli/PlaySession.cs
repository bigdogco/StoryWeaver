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

    /// <summary>
    /// Where the pack lives. Content, as opposed to <see cref="SaveRoot"/>, which is state —
    /// the split that lets a world be edited between sessions and shared without somebody's
    /// playthrough inside it. Only lore reads from here so far.
    /// </summary>
    private const string PackRoot = "worlds";

    public static async Task<int> RunAsync(StoryWeaverSettings settings)
    {
        FileLlmLog log = new(settings.Logging);
        using OpenRouterClient client = new(settings, log);

        // Authored content, loaded once. A malformed entry throws by name and line rather
        // than vanishing — a silently dropped lore entry is the failure this genre is worst
        // at, and it is the one thing here worth failing a startup over.
        LoreBook lore = MarkdownLoreReader.Load(Path.Combine(PackRoot, WorldId, "lore"));

        JsonWorldRepository repository = new(SaveRoot);
        int historyTurns = settings.Story.HistoryTurns;
        TurnEngine engine = new(
            new LlmNarrator(client),
            new LlmStateExtractor(client),
            repository,
            historyTurns,
            lore);

        // Resume the world if it exists, otherwise seed it and write the first save so the
        // world is on disk before any turn runs.
        WorldState? loaded = await repository.LoadAsync(WorldId).ConfigureAwait(false);
        bool resumed = loaded is not null;
        WorldState world = loaded ?? WorldSeeds.Marrow();

        if (!resumed)
        {
            await repository.SaveAsync(WorldId, world).ConfigureAwait(false);
        }

        PrintBanner(log.FilePath, repository.RootDirectory, resumed, world.TurnNumber, historyTurns, lore);

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
                        .TryHandleAsync(input, WorldId, world, repository, lore)
                        .ConfigureAwait(false))
                {
                    HandleCommand(input, world, repository, lore);
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

    private static void HandleCommand(string input, WorldState world, IWorldRepository repo, LoreBook lore)
    {
        switch (input)
        {
            case "/lore":
                PrintLore(world, lore);
                break;

            case "/state":
                Console.WriteLine();
                Console.WriteLine(ContextAssembler.ForExtraction(world, lore));
                break;

            // Worth having its own command: this is the view that must contain no ids, and
            // eyeballing it is the only check that the narrator cannot leak one into prose.
            case "/prose":
                Console.WriteLine();
                Console.WriteLine(ContextAssembler.ForNarration(world, lore));
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
                Console.WriteLine("  /lore   the pack's lore, and who has heard of what");
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
                Console.WriteLine("  /knows      let a character have heard of a lore entry");
                break;

            default:
                Console.WriteLine($"Unknown command '{input}'. Try /help.");
                break;
        }
    }

    /// <summary>
    /// The pack's lore, and who has heard of each entry.
    ///
    /// Read-only, unlike the other authoring commands. Lore is authored in files, so a
    /// <c>/lore add</c> would be a second way to write content that the Lore Writer window
    /// will eventually own — and two writers of the same files is how a format starts
    /// disagreeing with itself.
    ///
    /// The knowledge column is the part worth looking at: it is the feature's whole premise,
    /// and the only way to see that an NPC is being kept ignorant on purpose.
    /// </summary>
    private static void PrintLore(WorldState world, LoreBook lore)
    {
        Console.WriteLine();

        if (lore.Count == 0)
        {
            Console.WriteLine("  No lore entries. Add markdown files under worlds/marrow/lore/.");
            return;
        }

        foreach (LoreEntry entry in lore.All)
        {
            // Only the stored side is listed per name. A common entry is known by everyone by
            // definition, and printing the whole cast under it would bury the distinction
            // that matters — who was *told*, versus who simply lives here.
            List<string> knownBy =
            [
                .. world.Characters.Values
                    .Where(c => c.Knows.Contains(entry.Id))
                    .Select(c => c.Name)
                    .OrderBy(n => n, StringComparer.Ordinal),
            ];

            List<string> flags = [];

            if (entry.Always)
            {
                flags.Add("always");
            }

            if (entry.Common)
            {
                flags.Add("common");
            }

            Console.WriteLine($"  {entry.Title}  ({entry.Id})");
            Console.WriteLine($"    priority {entry.Priority}"
                              + (flags.Count == 0 ? string.Empty : $", {string.Join(", ", flags)}"));

            if (entry.Keys.Count > 0)
            {
                Console.WriteLine($"    keys: {string.Join(", ", entry.Keys)}");
            }

            if (entry.Common)
            {
                Console.WriteLine(knownBy.Count == 0
                    ? "    heard of by: everyone (common knowledge)"
                    : $"    heard of by: everyone (common knowledge); told directly: {string.Join(", ", knownBy)}");
            }
            else
            {
                Console.WriteLine(knownBy.Count == 0
                    ? "    heard of by: nobody"
                    : $"    heard of by: {string.Join(", ", knownBy)}");
            }

            Console.WriteLine();
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
        int historyTurns,
        LoreBook lore)
    {
        Console.WriteLine();
        Console.WriteLine(resumed
            ? $"StoryWeaver — play session (resumed at turn {turnNumber})"
            : "StoryWeaver — play session (new world)");
        Console.WriteLine($"Saving to  {saveRoot}");
        Console.WriteLine($"Logging to {logPath}");
        Console.WriteLine($"Narrator remembers the last {historyTurns} turns");

        // Stated even when zero. "No lore loaded" is information; an absent line reads as a
        // feature that is not there, which is how a mistyped pack path stays invisible.
        Console.WriteLine(lore.Count == 0
            ? "No lore entries loaded"
            : $"Lore: {lore.Count} entr{(lore.Count == 1 ? "y" : "ies")} — {string.Join(", ", lore.Ids)}");
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
