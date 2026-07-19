using System.Text.Json.Serialization;

namespace StoryWeaver.Llm.Configuration;

/// <summary>
/// Root of settings.local.json. Deserialized with System.Text.Json; unknown properties
/// (such as the "_comment" keys in the settings file) are ignored by default.
/// </summary>
public sealed class StoryWeaverSettings
{
    [JsonPropertyName("provider")]
    public ProviderSettings Provider { get; init; } = new();

    /// <summary>
    /// Role name -> settings. A dictionary rather than fixed properties so adding a role
    /// later (image, for instance) is a config entry rather than a refactor. Keys are
    /// matched case-insensitively against <see cref="LlmRole"/> names.
    ///
    /// Settable rather than init-only, and deliberately so: System.Text.Json *replaces*
    /// a collection property during deserialization instead of populating the existing
    /// instance, which silently discards the comparer set here. <see cref="SettingsLoader"/>
    /// rebuilds this dictionary with the case-insensitive comparer after loading.
    /// </summary>
    [JsonPropertyName("roles")]
    public Dictionary<string, RoleSettings> Roles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("logging")]
    public LoggingSettings Logging { get; init; } = new();

    /// <summary>
    /// Look up a role's settings, or throw with a message naming what is configured.
    /// </summary>
    public RoleSettings GetRole(LlmRole role)
    {
        if (Roles.TryGetValue(role.ToString(), out RoleSettings? settings))
        {
            return settings;
        }

        string configured = Roles.Count == 0 ? "(none)" : string.Join(", ", Roles.Keys);
        throw new SettingsException(
            $"No configuration for role '{role}'. Configured roles: {configured}.");
    }
}

public sealed class ProviderSettings
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; init; } = "https://openrouter.ai/api/v1/chat/completions";

    /// <summary>Optional. OpenRouter uses this for app attribution on their listings.</summary>
    [JsonPropertyName("httpReferer")]
    public string HttpReferer { get; init; } = string.Empty;

    /// <summary>Optional. Display name on OpenRouter's listings.</summary>
    [JsonPropertyName("xTitle")]
    public string XTitle { get; init; } = "StoryWeaver";

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; init; } = 120;
}

/// <summary>
/// Maps to OpenRouter's <c>reasoning</c> parameter.
///
/// <b>This is a parameter, so the routing hazard applies to it.</b> Sent without
/// <c>require_parameters: true</c>, a provider that does not support it ignores it — you
/// would get full-effort reasoning at full cost while believing effort was set to "low".
/// Identical failure shape to <c>response_format</c>, identical mitigation.
///
/// Note that <see cref="Exclude"/> saves nothing: it strips reasoning from the response,
/// not from the bill or the token budget. <see cref="Effort"/> is the cost control.
/// </summary>
public sealed class ReasoningSettings
{
    /// <summary>One of max, xhigh, high, medium, low, minimal, none. "none" disables
    /// reasoning where supported; models with mandatory reasoning reject it.</summary>
    [JsonPropertyName("effort")]
    public string? Effort { get; init; }

    /// <summary>Explicit reasoning budget. Supported by Gemini thinking models, Anthropic,
    /// and some Qwen models; on effort-only models it is converted to an effort level.</summary>
    [JsonPropertyName("maxTokens")]
    public int? MaxTokens { get; init; }

    /// <summary>Keep reasoning active but omit it from the response. Does not reduce cost
    /// or free up budget.</summary>
    [JsonPropertyName("exclude")]
    public bool? Exclude { get; init; }
}

public sealed class RoleSettings
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("temperature")]
    public float Temperature { get; init; } = 0.7f;

    /// <summary>
    /// Total output budget. <b>On a reasoning model this must cover the thinking as well as
    /// the answer</b> — reasoning tokens are billed as output tokens and drawn from this
    /// same allowance. A budget sized for the answer alone yields an empty response with no
    /// error and a bland <c>finish_reason: "length"</c>. See docs/CHALLENGES.md.
    /// </summary>
    [JsonPropertyName("maxTokens")]
    public int MaxTokens { get; init; } = 1000;

    /// <summary>Optional reasoning control. Omitted from the request entirely when null,
    /// leaving the model at its own default.</summary>
    [JsonPropertyName("reasoning")]
    public ReasoningSettings? Reasoning { get; init; }

    /// <summary>
    /// Maps to OpenRouter's <c>provider.require_parameters</c>. When true, routing is
    /// restricted to upstream providers that support every parameter sent.
    ///
    /// This is not optional hardening for any role using <see cref="LlmResponseFormat.JsonSchema"/>.
    /// OpenRouter load-balances one model ID across several providers, and a provider that
    /// does not support a parameter *silently ignores it* — no error. Without this flag,
    /// extraction intermittently returns prose instead of JSON depending on which provider
    /// served the request. See docs/CHALLENGES.md.
    /// </summary>
    [JsonPropertyName("requireParameters")]
    public bool RequireParameters { get; init; }

    /// <summary>
    /// Written snake_case in config ("json_schema") to match OpenRouter's own vocabulary.
    /// The converter handling that is registered in <see cref="SettingsLoader"/>.
    /// </summary>
    [JsonPropertyName("responseFormat")]
    public LlmResponseFormat ResponseFormat { get; init; } = LlmResponseFormat.Text;
}

public sealed class LoggingSettings
{
    [JsonPropertyName("logPrompts")]
    public bool LogPrompts { get; init; } = true;

    [JsonPropertyName("logResponses")]
    public bool LogResponses { get; init; } = true;

    [JsonPropertyName("logDirectory")]
    public string LogDirectory { get; init; } = "logs";
}

/// <summary>
/// Thrown when settings are missing, unreadable, or invalid. Carries every problem found
/// rather than only the first, so one run surfaces the whole list.
/// </summary>
public sealed class SettingsException : Exception
{
    public SettingsException(string message) : base(message) { }

    public SettingsException(string message, Exception inner) : base(message, inner) { }
}
