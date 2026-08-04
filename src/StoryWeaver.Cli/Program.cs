using System.Text.Json;
using StoryWeaver.Llm;
using StoryWeaver.Llm.Configuration;
using StoryWeaver.Llm.Logging;
using StoryWeaver.Llm.OpenRouter;

namespace StoryWeaver.Cli;

/// <summary>
/// Throwaway harness. Loads and validates settings, and can run a smoke test that exercises
/// both roles against the real API.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("StoryWeaver - console harness");
        Console.WriteLine();

        // Ahead of settings loading: it makes no API calls and must stay runnable when
        // nothing is configured, so a fresh clone can check serialization before signing up
        // for an API key.
        if (args.Contains("--selftest", StringComparer.OrdinalIgnoreCase))
        {
            // Both suites run; the exit code is the worse of the two, so a lore failure
            // cannot be hidden by serialization passing.
            int json = JsonSelfTest.Run();
            Console.WriteLine();
            int lore = LoreSelfTest.Run();
            Console.WriteLine();
            int wire = StoryWeaver.Llm.OpenRouter.ResponseSelfTest.Run();
            return Math.Max(json, Math.Max(lore, wire));
        }

        // Writes the built-in seed out as a pack file. One-shot authoring aid: it guarantees
        // the JSON seed and the C# fixture start identical, which is the only way to be sure
        // the move to packs changed nothing.
        if (args.Contains("--write-seed", StringComparer.OrdinalIgnoreCase))
        {
            return SeedWriter.Run(Value(args, "--write-seed-to") ?? "worlds/marrow/seed.json");
        }

        bool smoke = args.Contains("--smoke", StringComparer.OrdinalIgnoreCase);
        bool probe = args.Contains("--probe-schema", StringComparer.OrdinalIgnoreCase);
        // Skip anything that is the value of a --flag, or "--models deepseek/x" would be read
        // as a settings file path and the real settings silently ignored.
        string[] valueFlags = ["--models", "--runs", "--scenarios", "--providers"];
        string? settingsPath = args
            .Where((a, i) => !a.StartsWith("--", StringComparison.Ordinal)
                             && (i == 0 || !valueFlags.Contains(args[i - 1], StringComparer.OrdinalIgnoreCase)))
            .FirstOrDefault();

        StoryWeaverSettings settings;
        try
        {
            settings = SettingsLoader.Load(settingsPath);
        }
        catch (SettingsException ex)
        {
            Console.Error.WriteLine("Settings error:");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        PrintSettings(settings);

        if (args.Contains("--eval", StringComparer.OrdinalIgnoreCase))
        {
            string[] models = Value(args, "--models")?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                              ?? [settings.GetRole(LlmRole.Extraction).Model];
            int runs = int.TryParse(Value(args, "--runs"), out int parsed) ? parsed : 3;
            bool showDeltas = args.Contains("--show-deltas", StringComparer.OrdinalIgnoreCase);

            // Default to the scored set. Naming scenarios explicitly also reaches the
            // diagnostics, which are kept out of the scored set on purpose.
            string? names = Value(args, "--scenarios");
            IReadOnlyList<EvalScenario> scenarios;

            if (names is null)
            {
                scenarios = EvalScenarios.All;
            }
            else
            {
                string[] wanted = names.Split(',', StringSplitOptions.RemoveEmptyEntries);
                scenarios =
                [
                    .. EvalScenarios.Everything
                        .Where(s => wanted.Contains(s.Name, StringComparer.OrdinalIgnoreCase)),
                ];

                string[] unknown =
                [
                    .. wanted.Where(w =>
                        !EvalScenarios.Everything.Any(s =>
                            string.Equals(s.Name, w, StringComparison.OrdinalIgnoreCase))),
                ];

                if (unknown.Length > 0)
                {
                    Console.Error.WriteLine($"Unknown scenario(s): {string.Join(", ", unknown)}");
                    Console.Error.WriteLine(
                        $"Available: {string.Join(", ", EvalScenarios.Everything.Select(s => s.Name))}");
                    return 1;
                }
            }

            string[]? providers = Value(args, "--providers")
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries);

            return await ExtractionEval.RunAsync(settings, models, runs, scenarios, showDeltas, providers)
                .ConfigureAwait(false);
        }

        if (args.Contains("--play", StringComparer.OrdinalIgnoreCase))
        {
            return await PlaySession.RunAsync(settings).ConfigureAwait(false);
        }

        if (probe)
        {
            return await DeltaSchemaProbe.RunAsync(settings).ConfigureAwait(false);
        }

        if (smoke)
        {
            return await RunSmokeTestAsync(settings).ConfigureAwait(false);
        }

        Console.WriteLine("  --play          play a session (saved to disk, two calls per turn)");
        Console.WriteLine("  --smoke         live API test, two real calls");
        Console.WriteLine("  --probe-schema  live test of the nine-branch delta schema, one real call");
        Console.WriteLine("  --selftest      offline serialization checks, no API");
        Console.WriteLine("  --eval          score extraction models against fixed scenarios");
        Console.WriteLine("                    --models a,b   models to compare (default: configured)");
        Console.WriteLine("                    --runs N       runs per scenario (default: 3)");
        Console.WriteLine("                    --scenarios x,y  named scenarios, incl. diagnostics");
        Console.WriteLine("                    --providers a,b  sample each upstream separately");
        Console.WriteLine("                    --show-deltas  print what each run actually proposed");
        return 0;
    }

    /// <summary>Reads <c>--flag value</c>, returning null when absent or valueless.</summary>
    private static string? Value(string[] args, string flag)
    {
        int index = Array.FindIndex(args, a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void PrintSettings(StoryWeaverSettings settings)
    {
        Console.WriteLine("Settings loaded and validated.");
        Console.WriteLine();
        Console.WriteLine($"  Endpoint : {settings.Provider.BaseUrl}");
        Console.WriteLine($"  API key  : {Mask(settings.Provider.ApiKey)}");
        Console.WriteLine($"  Timeout  : {settings.Provider.TimeoutSeconds}s");
        Console.WriteLine();
        Console.WriteLine("  Roles:");

        foreach ((string name, RoleSettings role) in settings.Roles.OrderBy(r => r.Key))
        {
            string model = string.IsNullOrWhiteSpace(role.Model) ? "(unset)" : role.Model;
            string guard = role.RequireParameters ? ", require_parameters" : string.Empty;
            Console.WriteLine(
                $"    {name,-12} {model}  [temp {role.Temperature}, {role.ResponseFormat}{guard}]");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Two calls. Narration proves the basic path; extraction proves schema-constrained
    /// output actually works on the configured model, which is the open question the
    /// bootstrap phase exists to answer.
    /// </summary>
    private static async Task<int> RunSmokeTestAsync(StoryWeaverSettings settings)
    {
        FileLlmLog log = new(settings.Logging);
        Console.WriteLine($"Logging to {log.FilePath}");
        Console.WriteLine();

        using OpenRouterClient client = new(settings, log);

        Console.WriteLine("[1/2] Narration ...");
        LlmResult narration = await client.CompleteAsync(new LlmCall
        {
            Role = LlmRole.Narration,
            Messages =
            [
                LlmMessage.System("You are narrating a dark fantasy RPG. Two sentences, no preamble."),
                LlmMessage.User("The player pushes open the door of the tavern in Marrow."),
            ],
        }).ConfigureAwait(false);

        Report(narration);

        if (!narration.IsSuccess)
        {
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("[2/2] Extraction (schema-constrained) ...");

        const string schema = """
        {
          "type": "object",
          "properties": {
            "location":  { "type": "string", "description": "Where the scene takes place." },
            "characters": {
              "type": "array",
              "description": "Named characters present.",
              "items": { "type": "string" }
            },
            "mood": { "type": "string", "description": "One word for the scene's tone." }
          },
          "required": ["location", "characters", "mood"],
          "additionalProperties": false
        }
        """;

        LlmResult extraction = await client.CompleteAsync(new LlmCall
        {
            Role = LlmRole.Extraction,
            Schema = new JsonSchemaSpec("scene_state", schema),
            Validator = IsJsonObject,
            Messages =
            [
                LlmMessage.System("Extract structured state from the narration. Return JSON only."),
                LlmMessage.User(narration.Content),
            ],
        }).ConfigureAwait(false);

        Report(extraction);

        if (extraction.IsSuccess)
        {
            Console.WriteLine();
            Console.WriteLine("  Schema-constrained output works on this model.");
        }

        return extraction.IsSuccess ? 0 : 1;
    }

    private static void Report(LlmResult result)
    {
        if (!result.IsSuccess)
        {
            Console.WriteLine($"  FAILED after {result.Attempts} attempt(s): {result.Error}");
            return;
        }

        Console.WriteLine($"  {result.Content.Trim()}");
        Console.WriteLine();
        Console.WriteLine(
            $"  served by {result.Model ?? "(unreported)"}, " +
            $"{result.Usage?.TotalTokens ?? 0} tokens, {result.Attempts} attempt(s)");
    }

    /// <summary>Cheap structural check — is this parseable JSON with an object at the root?</summary>
    private static bool IsJsonObject(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Never print the key. Enough characters to tell which key it is, no more.</summary>
    private static string Mask(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return "(empty)";
        }

        return key.Length <= 8
            ? new string('*', key.Length)
            : $"{key[..4]}...{key[^4..]}";
    }
}
