using System.Text.Json;
using System.Text.Json.Serialization;

namespace StoryWeaver.Llm.Configuration;

/// <summary>
/// Loads and validates settings. Validation runs at startup and fails loudly, rather than
/// letting a missing key or an unset model surface as a confusing failure on the first API
/// call several turns into a session.
/// </summary>
public static class SettingsLoader
{
    public const string DefaultFileName = "settings.local.json";

    /// <summary>Environment variables checked for the API key, in order. Either overrides
    /// the value in the settings file, which is useful for CI or for keeping the key out
    /// of a file entirely.</summary>
    private static readonly string[] ApiKeyEnvVars = ["STORYWEAVER_API_KEY", "OPENROUTER_API_KEY"];

    /// <summary>Roles that must be configured for the application to start. Summarize and
    /// worldgen are reserved and not yet used, so they are not required.</summary>
    private static readonly LlmRole[] RequiredRoles = [LlmRole.Narration, LlmRole.Extraction];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            // Enum values are written snake_case in config ("json_schema") so the file
            // reads in OpenRouter's own vocabulary rather than C#'s.
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
        },
    };

    /// <summary>
    /// Load settings from an explicit path, or by searching upward from the executable for
    /// <see cref="DefaultFileName"/>.
    /// </summary>
    public static StoryWeaverSettings Load(string? path = null)
    {
        string resolved = path ?? FindSettingsFile()
            ?? throw new SettingsException(
                $"Could not find {DefaultFileName}. Searched upward from " +
                $"'{AppContext.BaseDirectory}'. Copy settings.example.json to " +
                $"{DefaultFileName} in the repository root and fill it in.");

        string json;
        try
        {
            json = File.ReadAllText(resolved);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SettingsException($"Could not read settings file '{resolved}'.", ex);
        }

        StoryWeaverSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<StoryWeaverSettings>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new SettingsException(
                $"Settings file '{resolved}' is not valid JSON: {ex.Message}", ex);
        }

        if (settings is null)
        {
            throw new SettingsException($"Settings file '{resolved}' deserialized to null.");
        }

        // System.Text.Json replaced the Roles dictionary during deserialization, dropping
        // the case-insensitive comparer it was constructed with. Restore it, so config keys
        // ("narration") match enum names ("Narration") regardless of casing.
        settings.Roles = new Dictionary<string, RoleSettings>(settings.Roles, StringComparer.OrdinalIgnoreCase);

        ApplyEnvironmentOverrides(settings);
        Validate(settings, resolved);

        return settings;
    }

    /// <summary>
    /// Walk up from the executable looking for the settings file. This keeps the real file
    /// at the repository root — where it is gitignored and easy to edit — rather than
    /// requiring a copy-to-output step and a rebuild after every change.
    /// </summary>
    private static string? FindSettingsFile()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);

        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, DefaultFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static void ApplyEnvironmentOverrides(StoryWeaverSettings settings)
    {
        foreach (string name in ApiKeyEnvVars)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                settings.Provider.ApiKey = value;
                return;
            }
        }
    }

    /// <summary>
    /// Collect every problem before throwing, so one run tells you everything that is wrong.
    /// </summary>
    private static void Validate(StoryWeaverSettings settings, string path)
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(settings.Provider.ApiKey))
        {
            errors.Add(
                "provider.apiKey is empty. Set it in the settings file, or set the " +
                $"{ApiKeyEnvVars[0]} environment variable.");
        }

        if (!Uri.TryCreate(settings.Provider.BaseUrl, UriKind.Absolute, out _))
        {
            errors.Add($"provider.baseUrl is not a valid absolute URL: '{settings.Provider.BaseUrl}'.");
        }

        if (settings.Provider.TimeoutSeconds <= 0)
        {
            errors.Add($"provider.timeoutSeconds must be greater than 0 (was {settings.Provider.TimeoutSeconds}).");
        }

        foreach (LlmRole role in RequiredRoles)
        {
            if (!settings.Roles.TryGetValue(role.ToString(), out RoleSettings? roleSettings))
            {
                errors.Add($"roles.{ToKey(role)} is missing.");
                continue;
            }

            ValidateRole(role, roleSettings, errors);
        }

        if (errors.Count > 0)
        {
            string detail = string.Join(Environment.NewLine, errors.Select(e => "  - " + e));
            throw new SettingsException(
                $"Settings file '{path}' has {errors.Count} problem(s):{Environment.NewLine}{detail}");
        }
    }

    private static void ValidateRole(LlmRole role, RoleSettings settings, List<string> errors)
    {
        string key = ToKey(role);

        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            errors.Add($"roles.{key}.model is empty. Set an OpenRouter model ID.");
        }

        if (settings.MaxTokens <= 0)
        {
            errors.Add($"roles.{key}.maxTokens must be greater than 0 (was {settings.MaxTokens}).");
        }

        if (settings.Temperature is < 0f or > 2f)
        {
            errors.Add($"roles.{key}.temperature should be between 0 and 2 (was {settings.Temperature}).");
        }

        // The coupling that motivated this validator existing at all. See docs/CHALLENGES.md:
        // json_schema without require_parameters means OpenRouter may route to a provider
        // that ignores response_format entirely and returns prose, with no error to catch.
        if (settings.ResponseFormat == LlmResponseFormat.JsonSchema && !settings.RequireParameters)
        {
            errors.Add(
                $"roles.{key} uses responseFormat 'JsonSchema' but requireParameters is false. " +
                "OpenRouter may route to a provider that silently ignores response_format, " +
                "so the call would intermittently return prose instead of JSON with no error. " +
                "Either set requireParameters to true, or change responseFormat to 'JsonObject' " +
                "if the chosen model does not support schema-constrained output.");
        }

        if (settings.Reasoning is null)
        {
            return;
        }

        // Same hazard, different parameter. A silently-dropped `reasoning` block is worse
        // than a dropped response_format in one respect: the output still looks correct, so
        // nothing prompts you to check. You just quietly pay for reasoning you switched off.
        if (!settings.RequireParameters)
        {
            errors.Add(
                $"roles.{key} configures reasoning but requireParameters is false. " +
                "OpenRouter may route to a provider that ignores the reasoning parameter, " +
                "leaving the model at full effort and full cost with no visible symptom. " +
                "Set requireParameters to true.");
        }

        if (settings.Reasoning.Effort is { } effort && !ValidEfforts.Contains(effort))
        {
            errors.Add(
                $"roles.{key}.reasoning.effort is '{effort}'. Valid values: " +
                $"{string.Join(", ", ValidEfforts)}.");
        }

        if (settings.Reasoning.MaxTokens is <= 0)
        {
            errors.Add(
                $"roles.{key}.reasoning.maxTokens must be greater than 0 " +
                $"(was {settings.Reasoning.MaxTokens}).");
        }

        if (settings.Reasoning.MaxTokens >= settings.MaxTokens)
        {
            errors.Add(
                $"roles.{key}.reasoning.maxTokens ({settings.Reasoning.MaxTokens}) leaves no " +
                $"room under maxTokens ({settings.MaxTokens}). Reasoning is drawn from the same " +
                "budget as the answer, so the model would exhaust it before writing output.");
        }
    }

    private static readonly string[] ValidEfforts =
        ["max", "xhigh", "high", "medium", "low", "minimal", "none"];

    /// <summary>Config keys are camelCase; enum names are PascalCase.</summary>
    private static string ToKey(LlmRole role)
    {
        string name = role.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
