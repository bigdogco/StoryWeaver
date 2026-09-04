using StoryWeaver.Harness;
using StoryWeaver.Llm;
using StoryWeaver.Llm.Configuration;

namespace StoryWeaver.Cli;

/// <summary>
/// The console entry point — a dispatcher. It loads and validates settings, then hands off:
/// play to <see cref="PlaySession"/>, and everything instrumental (<c>--selftest</c>,
/// <c>--eval</c>, <c>--smoke</c>, <c>--probe-schema</c>, <c>--write-seed</c>) to the Harness.
///
/// It renders and prompts and nothing more. The gameplay lives in Core, the composition in App,
/// and the instrumentation in Harness; this file is a thin console client of all three.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("StoryWeaver - console client");
        Console.WriteLine();

        // Ahead of settings loading: it makes no API calls and must stay runnable when
        // nothing is configured, so a fresh clone can check serialization before signing up
        // for an API key.
        if (args.Contains("--selftest", StringComparer.OrdinalIgnoreCase))
        {
            // The suites live in the Harness now; it owns the order and how their codes combine.
            return SelfTests.RunAll();
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
        string[] valueFlags = ["--models", "--runs", "--scenarios", "--providers", "--pack", "--save"];
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

            // The Harness scores and reports progress to the observer; the CLI renders both the
            // running commentary and the final report. Two halves of one seam.
            ConsoleEvalObserver observer = new(showDeltas);
            EvalReport report = await ExtractionEval
                .RunAsync(settings, models, runs, scenarios, providers, observer)
                .ConfigureAwait(false);

            EvalRenderer.RenderSummary(report);
            return 0;
        }

        if (args.Contains("--play", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                return await PlaySession
                    .RunAsync(
                        settings,
                        Value(args, "--pack"),
                        Value(args, "--save"),
                        args.Contains("--force", StringComparer.OrdinalIgnoreCase))
                    .ConfigureAwait(false);
            }
            catch (InvalidDataException ex)
            {
                // Pack content is authored by hand, so a mistake in it is a user error rather
                // than a defect — a stack trace tells somebody who mistyped a lore key nothing
                // they can act on. Reported like a settings error, which has the same shape.
                Console.Error.WriteLine("Pack error:");
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        if (probe)
        {
            return await DeltaSchemaProbe.RunAsync(settings).ConfigureAwait(false);
        }

        if (smoke)
        {
            return await SmokeTest.RunAsync(settings).ConfigureAwait(false);
        }

        Console.WriteLine("  --play          play a session (saved to disk, two calls per turn)");
        Console.WriteLine("                    --pack id      world to play (default: marrow)");
        Console.WriteLine("                    --save id      playthrough to use (default: the pack's id)");
        Console.WriteLine("                    --force        open a save another session still holds");
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
