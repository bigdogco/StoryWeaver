using StoryWeaver.Llm.Configuration;

namespace StoryWeaver.Cli;

/// <summary>
/// Throwaway harness. For now it does one thing: load and validate settings, then report
/// what it found. That is enough to confirm the config path works end to end before any
/// domain code exists.
/// </summary>
internal static class Program
{
    /// <param name="args">
    /// Optional: a path to a settings file. Defaults to searching upward for
    /// settings.local.json. Useful for trying an alternate config without editing
    /// the real one.
    /// </param>
    private static int Main(string[] args)
    {
        Console.WriteLine("StoryWeaver - console harness");
        Console.WriteLine();

        string? settingsPath = args.Length > 0 ? args[0] : null;

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
        Console.WriteLine("Nothing else is wired up yet - next step is the LLM client.");
        return 0;
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
