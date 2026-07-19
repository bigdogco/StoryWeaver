using StoryWeaver.Core;
using StoryWeaver.Llm.Configuration;
using StoryWeaver.Llm.Logging;
using StoryWeaver.Llm.OpenRouter;
using StoryWeaver.Llm.Story;

namespace StoryWeaver.Cli;

/// <summary>
/// Playable console harness. Throwaway, and storage is in-memory — a session dies with the
/// process until section 6 lands.
///
/// The point of this is not the game. It is watching what extraction does over many turns:
/// the rejection list is printed after every turn precisely because a silently dropped
/// delta is the failure mode that would otherwise take fifty turns to notice.
/// </summary>
internal static class PlaySession
{
    private const string WorldId = "marrow";

    public static async Task<int> RunAsync(StoryWeaverSettings settings)
    {
        FileLlmLog log = new(settings.Logging);
        using OpenRouterClient client = new(settings, log);

        InMemoryWorldRepository repository = new();
        TurnEngine engine = new(new LlmNarrator(client), new LlmStateExtractor(client), repository);

        WorldState world = SeedWorld();
        await repository.SaveAsync(WorldId, world).ConfigureAwait(false);

        PrintBanner(log.FilePath);
        PrintOpeningScene(world);

        while (true)
        {
            Console.Write("\n> ");
            string? input = Console.ReadLine();

            if (input is null || input.Trim() is "/quit" or "/q")
            {
                Console.WriteLine("\nEnding session. Nothing was saved — storage is in-memory.");
                return 0;
            }

            input = input.Trim();

            if (input.Length == 0)
            {
                continue;
            }

            if (input.StartsWith('/'))
            {
                HandleCommand(input, world, repository);
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

    private static void PrintTurn(TurnOutcome outcome)
    {
        Console.WriteLine(outcome.Turn.Narration);
        Console.WriteLine();

        if (outcome.ExtractionFailed)
        {
            // Distinct from "extraction ran and produced nothing usable". The story continued
            // but canon did not move, and a run of these means drift.
            Console.WriteLine($"  [!] Extraction failed: {outcome.ExtractionError}");
            Console.WriteLine("  [!] Canon did not change this turn.");
            return;
        }

        Console.WriteLine($"  --- turn {outcome.Turn.TurnNumber} ---");

        if (outcome.Turn.Applied.Count == 0)
        {
            Console.WriteLine("  applied: nothing");
        }
        else
        {
            foreach (StateDelta delta in outcome.Turn.Applied)
            {
                Console.WriteLine($"  applied:  {Describe(delta)}");
            }
        }

        foreach (StateDelta delta in outcome.Turn.NoOps)
        {
            Console.WriteLine($"  no-op:    {Describe(delta)} (already true)");
        }

        foreach (RejectedDelta rejected in outcome.Turn.Rejected)
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
        LocationIntroduced d => $"new location {d.LocationId} ({d.Name})",
        _ => delta.GetType().Name,
    };

    private static void HandleCommand(string input, WorldState world, InMemoryWorldRepository repo)
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
                Console.WriteLine("  /state  world state as the extractor sees it (with ids)");
                Console.WriteLine("  /prose  world state as the narrator sees it (no ids)");
                Console.WriteLine("  /raw    last raw extraction response");
                Console.WriteLine("  /quit   end the session");
                break;

            default:
                Console.WriteLine($"Unknown command '{input}'. Try /help.");
                break;
        }
    }

    private static void PrintLastRaw(InMemoryWorldRepository repo)
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

    private static void PrintBanner(string logPath)
    {
        Console.WriteLine();
        Console.WriteLine("StoryWeaver — play session (in-memory, nothing is saved)");
        Console.WriteLine($"Logging to {logPath}");
        Console.WriteLine("Commands: /state  /raw  /help  /quit");
        Console.WriteLine();
    }

    private static void PrintOpeningScene(WorldState world)
    {
        Location tavern = world.Locations["marrow-tavern"];
        Console.WriteLine(new string('-', 70));
        Console.WriteLine(tavern.Description);
        Console.WriteLine(new string('-', 70));
    }

    /// <summary>
    /// Hardcoded opening. One location the player can leave toward, two characters, and one
    /// fact that exactly one of them knows — enough to see whether knowledge stays where it
    /// belongs, which a scene with no secrets could not show.
    /// </summary>
    private static WorldState SeedWorld()
    {
        WorldState world = new();

        world.Locations["marrow-tavern"] = new Location
        {
            Id = "marrow-tavern",
            Name = "The Drowned Crow",
            Description =
                "A low-ceilinged taproom in the town of Marrow. Peat smoke, spilled ale, and " +
                "the sour cold that comes off the marsh outside. A handful of locals keep to " +
                "their own tables and their own business.",
            Connections = { "marrow-square" },
        };

        world.Locations["marrow-square"] = new Location
        {
            Id = "marrow-square",
            Name = "Marrow Square",
            Description = "A rutted market square, mostly empty. The well at its centre is boarded over.",
            Connections = { "marrow-tavern" },
        };

        world.Characters[Character.PlayerId] = new Character
        {
            Id = Character.PlayerId,
            Name = "You",
            Description = "A traveller, recently arrived in Marrow.",
            LocationId = "marrow-tavern",
        };

        world.Characters["innkeeper-hald"] = new Character
        {
            Id = "innkeeper-hald",
            Name = "Hald",
            Description =
                "The innkeeper of the Drowned Crow. Heavyset, watchful, wipes the same patch " +
                "of counter when he is thinking.",
            LocationId = "marrow-tavern",
            Status = "normal",
            Mood = "guarded",
            RelationshipToPlayer = new Relationship(-10, "suspicious of strangers"),
            Knows = { "well-boarded" },
        };

        world.Characters["drinker-mabb"] = new Character
        {
            Id = "drinker-mabb",
            Name = "Mabb",
            Description = "An old marsh-hand nursing a mug in the corner. Talks when drunk, which is often.",
            LocationId = "marrow-tavern",
            Status = "drunk",
            Mood = "maudlin",
            RelationshipToPlayer = new Relationship(0, "no strong feelings"),
        };

        world.Facts["well-boarded"] = new Fact
        {
            Id = "well-boarded",
            Text = "The well in Marrow Square was boarded over after something was found in it.",
            EstablishedTurn = 0,
        };

        return world;
    }
}
