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
    /// <summary>
    /// Which pack to play. Content — the authored world.
    /// </summary>
    private const string PackId = "marrow";

    /// <summary>
    /// Which save to play it in. State — this playthrough.
    ///
    /// The same string as <see cref="PackId"/> today, and a separate constant on purpose:
    /// they are different identifiers that have been sharing one name, and a pack supporting
    /// several saves is only a matter of choosing this at startup. Kept identical for now so
    /// existing saves keep working untouched.
    /// </summary>
    private const string SaveId = "marrow";

    private const string SaveRoot = "saves";

    /// <summary>
    /// Where packs live. Content, as opposed to <see cref="SaveRoot"/>, which is state — the
    /// split that lets a world be edited between sessions and shared without somebody's
    /// playthrough inside it.
    /// </summary>
    private const string PackRoot = "worlds";

    public static async Task<int> RunAsync(StoryWeaverSettings settings)
    {
        FileLlmLog log = new(settings.Logging);
        using OpenRouterClient client = new(settings, log);

        // Authored content, loaded once. Malformed content throws by name and line rather
        // than vanishing — a silently dropped lore entry or an unreadable seed is the failure
        // this genre is worst at, and the one thing worth failing a startup over.
        WorldPack pack = WorldPack.Load(PackRoot, PackId);

        JsonWorldRepository repository = new(SaveRoot);
        int historyTurns = settings.Story.HistoryTurns;
        TurnEngine engine = new(
            new LlmNarrator(client),
            new LlmStateExtractor(client),
            repository,
            historyTurns,
            pack.Lore,
            pack.Sheets);

        // Resume the save if it exists, otherwise start from the pack's seed and write the
        // first save so the world is on disk before any turn runs.
        //
        // The built-in seed is the fallback for a pack that ships none. It is a fixture, not
        // content: the eval scenarios need worlds derived by mutating a base, which is a thing
        // C# does well and JSON does not.
        WorldState? loaded = await repository.LoadAsync(SaveId).ConfigureAwait(false);
        bool resumed = loaded is not null;
        WorldState world = loaded ?? pack.Seed ?? WorldSeeds.Marrow();

        if (!resumed)
        {
            if (pack.AuthorsThePlayer)
            {
                AnnounceAuthoredPlayer(world);
            }
            else
            {
                CreateCharacter(world);
            }

            await repository.SaveAsync(SaveId, world).ConfigureAwait(false);
        }

        PrintBanner(log.FilePath, repository.RootDirectory, resumed, world.TurnNumber, historyTurns, pack);

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
                else if (string.Equals(input, "/reroll", StringComparison.OrdinalIgnoreCase))
                {
                    await RerollAsync(engine, repository, world).ConfigureAwait(false);
                }
                else if (!await AuthoringCommands
                        .TryHandleAsync(input, SaveId, world, repository, pack.Lore)
                        .ConfigureAwait(false))
                {
                    HandleCommand(input, world, repository, pack.Lore, pack.Sheets);
                }

                continue;
            }

            try
            {
                Console.WriteLine("\n(thinking...)\n");
                TurnOutcome outcome = await engine
                    .RunTurnAsync(SaveId, world, input)
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
    /// Say who the pack decided you are, when it shipped a <c>player.md</c>.
    ///
    /// Not decoration. The alternative is a session that simply never asks, which reads as the
    /// prompt having been forgotten — and "why didn't it ask my name" is a worse first
    /// impression than a line saying where the name came from. It also points at
    /// <c>/rename</c>, so an authored protagonist does not read as a locked one.
    /// </summary>
    private static void AnnounceAuthoredPlayer(WorldState world)
    {
        if (world.Player is not { } player)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"You are {player.Name}, as this world has it.");
        Console.WriteLine("  /rename if you would rather be someone else.");
        Console.WriteLine();
    }

    /// <summary>
    /// Name and describe the player's character, before turn one.
    ///
    /// **Skipped entirely when the pack ships `characters/player.md`** — see
    /// <see cref="WorldPack.AuthorsThePlayer"/>. The two write the same fields and the prompts
    /// run second, so running both means the pack's premise is destroyed by any answer given
    /// here.
    ///
    /// **Required, not skippable.** The seed used to ship a player called "You" whose one-line
    /// description nobody chose, which was harmless only while the name appeared nowhere but
    /// their own record. Character sheets show it to somebody else: an NPC whose sheet reads
    /// "curious about {{player}}" renders as "curious about You".
    ///
    /// Names are fixed — for the player exactly as for any authored character — so this is the
    /// same act the pack author performed for Hald, done by the person who owns this one.
    /// </summary>
    private static void CreateCharacter(WorldState world)
    {
        if (world.Player is not { } player)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Before you begin — who are you?");
        Console.WriteLine();

        while (true)
        {
            Console.Write("  Name: ");
            string? name = Console.ReadLine()?.Trim();

            if (!string.IsNullOrWhiteSpace(name))
            {
                player.Name = name;
                break;
            }

            Console.WriteLine("  A name is required. Everyone in this world will use it.");
        }

        Console.WriteLine();
        Console.WriteLine("  Describe yourself — appearance, manner, what you are good at.");
        Console.WriteLine("  The narrator reads this. Blank keeps the default.");
        Console.Write("  You are: ");

        string? description = Console.ReadLine()?.Trim();

        if (!string.IsNullOrWhiteSpace(description))
        {
            player.Description = description;
        }

        Console.WriteLine();
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
            await repository.LoadHistoryAsync(SaveId).ConfigureAwait(false);

        if (history.Count == 0)
        {
            Console.WriteLine("No turns to retry yet.");
            return;
        }

        Console.WriteLine("\n(re-extracting the last turn...)\n");

        try
        {
            TurnOutcome outcome = await engine
                .ReExtractAsync(SaveId, world, history[^1])
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

    /// <summary>
    /// Throw away the last turn's prose and narrate it again from the same input.
    ///
    /// The counterpart to <see cref="RetryExtractionAsync"/>, and the one to reach for when the
    /// story is wrong rather than the bookkeeping — the narrator put words in your mouth,
    /// misread the room, or returned something that was not prose at all.
    ///
    /// Only available on a turn that changed nothing, because undoing applied deltas needs a
    /// canon snapshot that does not exist yet. That covers about a quarter of turns, and every
    /// turn where narration failed outright.
    /// </summary>
    private static async Task RerollAsync(
        TurnEngine engine,
        IWorldRepository repository,
        WorldState world)
    {
        IReadOnlyList<TurnRecord> history =
            await repository.LoadHistoryAsync(SaveId).ConfigureAwait(false);

        if (history.Count == 0)
        {
            Console.WriteLine("No turns to reroll yet.");
            return;
        }

        Console.WriteLine("\n(narrating that turn again...)\n");

        try
        {
            RerollOutcome reroll = await engine
                .RerollAsync(SaveId, world, history[^1])
                .ConfigureAwait(false);

            if (reroll.WasRefused)
            {
                Console.WriteLine($"  Cannot reroll: {reroll.RefusedBecause}");
                return;
            }

            PrintTurn(reroll.Outcome!);
        }
        catch (StoryWeaverException ex)
        {
            Console.WriteLine($"Reroll failed: {ex.Message}");
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
            Console.WriteLine("  [!] extract this turn again without rewriting the prose,");
            Console.WriteLine("  [!] or /reroll to narrate the turn again from scratch.");
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
        FactEstablished d => $"fact {d.FactId}: {d.Text}" + (d.SourceId is null ? "" : $" (said by {d.SourceId})"),
        FactLearned d => $"{d.CharacterId} learned {d.FactId}",
        CharacterIntroduced d => $"new character {d.CharacterId} ({d.Name})",
        CharacterRenamed d => $"{d.CharacterId} is now called {d.Name}",
        LocationIntroduced d => $"new location {d.LocationId} ({d.Name})",
        ItemIntroduced d => $"new item {d.ItemId} ({d.Name})" + (d.HolderId is null ? $" @ {d.LocationId}" : $" held by {d.HolderId}"),
        ItemMoved d => $"{d.ItemId} -> {d.ToLocationId ?? d.ToHolderId}",
        ItemRenamed d => $"{d.ItemId} is now {d.Name}",
        ItemStatusChanged d => $"{d.ItemId} is {d.Status}",
        _ => delta.GetType().Name,
    };

    private static void HandleCommand(
        string input,
        WorldState world,
        IWorldRepository repo,
        LoreBook lore,
        IReadOnlyDictionary<string, CharacterSheet> sheets)
    {
        switch (input)
        {
            case "/lore":
                PrintLore(world, lore);
                break;

            case "/state":
                Console.WriteLine();
                Console.WriteLine(ContextAssembler.ForExtraction(world, lore, sheets));
                break;

            // Worth having its own command: this is the view that must contain no ids, and
            // eyeballing it is the only check that the narrator cannot leak one into prose.
            case "/prose":
                Console.WriteLine();
                Console.WriteLine(ContextAssembler.ForNarration(world, lore, sheets));
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
                Console.WriteLine("  /reroll narrate the last turn again — new prose");
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
        IReadOnlyList<TurnRecord> history = repo.LoadHistoryAsync(SaveId).GetAwaiter().GetResult();

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

        IReadOnlyList<TurnRecord> history = await repo.LoadHistoryAsync(SaveId).ConfigureAwait(false);

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
        WorldPack pack)
    {
        Console.WriteLine();
        Console.WriteLine(resumed
            ? $"StoryWeaver — play session (resumed at turn {turnNumber})"
            : "StoryWeaver — play session (new world)");
        Console.WriteLine($"Pack       {pack.Directory}");
        Console.WriteLine($"Saving to  {saveRoot}");
        Console.WriteLine($"Logging to {logPath}");
        Console.WriteLine($"Narrator remembers the last {historyTurns} turns");

        // Where the starting world came from. Only interesting on a new world, and worth
        // saying then: a pack whose seed failed to be found silently falls back to the
        // built-in fixture, and the two look identical from the opening scene.
        if (!resumed)
        {
            Console.WriteLine(pack.HasSeed
                ? $"Seeded from {Path.Combine(pack.Directory, WorldPack.SeedFile)}"
                : "Seeded from the built-in world (this pack ships no seed.json)");
        }

        // Stated even when zero. "No lore loaded" is information; an absent line reads as a
        // feature that is not there, which is how a mistyped pack path stays invisible.
        Console.WriteLine(pack.Lore.Count == 0
            ? "No lore entries loaded"
            : $"Lore: {pack.Lore.Count} entr{(pack.Lore.Count == 1 ? "y" : "ies")} — {string.Join(", ", pack.Lore.Ids)}");
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
