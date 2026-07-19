using System.Text.Json;
using System.Text.Json.Serialization;

namespace StoryWeaver.Llm.OpenRouter;

// Wire-format DTOs. Property names are the OpenAI-compatible snake_case values OpenRouter
// expects; these types exist only to be serialized and deserialized, and should not leak
// past OpenRouterClient.

internal sealed class OpenRouterRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<WireMessage> Messages { get; init; } = [];

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; init; }

    [JsonPropertyName("temperature")]
    public float Temperature { get; init; }

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WireResponseFormat? ResponseFormat { get; init; }

    /// <summary>
    /// Provider routing preferences. Carries <c>require_parameters</c>, which restricts
    /// routing to upstream providers supporting every parameter sent. See docs/CHALLENGES.md.
    /// </summary>
    [JsonPropertyName("provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WireProvider? Provider { get; init; }

    /// <summary>Reasoning control. Omitted entirely when the role does not configure it,
    /// which leaves the model at its own default.</summary>
    [JsonPropertyName("reasoning")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WireReasoning? Reasoning { get; init; }
}

internal sealed class WireReasoning
{
    [JsonPropertyName("effort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Effort { get; init; }

    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("exclude")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Exclude { get; init; }
}

internal sealed class WireMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

internal sealed class WireProvider
{
    [JsonPropertyName("require_parameters")]
    public bool RequireParameters { get; init; }
}

internal sealed class WireResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "json_object";

    [JsonPropertyName("json_schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WireJsonSchema? JsonSchema { get; init; }
}

internal sealed class WireJsonSchema
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("strict")]
    public bool Strict { get; init; } = true;

    /// <summary>The schema object itself, held as parsed JSON so it round-trips verbatim.</summary>
    [JsonPropertyName("schema")]
    public JsonElement Schema { get; init; }
}

internal sealed class OpenRouterResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("choices")]
    public List<WireChoice>? Choices { get; init; }

    [JsonPropertyName("usage")]
    public WireUsage? Usage { get; init; }

    [JsonPropertyName("error")]
    public WireError? Error { get; init; }

    /// <summary>First choice's text, or null when the response carried no content.</summary>
    public string? Content =>
        Choices is { Count: > 0 } ? Choices[0].Message?.Content : null;

    /// <summary>Why generation stopped. <c>"length"</c> alongside empty content is the
    /// signature of a reasoning model that spent its whole budget thinking.</summary>
    public string? FinishReason =>
        Choices is { Count: > 0 } ? Choices[0].FinishReason : null;
}

internal sealed class WireChoice
{
    [JsonPropertyName("message")]
    public WireMessage? Message { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }

    /// <summary>The provider's own finish reason, before OpenRouter normalizes it.
    /// Useful when diagnosing provider-specific behaviour.</summary>
    [JsonPropertyName("native_finish_reason")]
    public string? NativeFinishReason { get; init; }
}

internal sealed class WireUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }

    [JsonPropertyName("completion_tokens_details")]
    public WireCompletionDetails? CompletionDetails { get; init; }
}

internal sealed class WireCompletionDetails
{
    /// <summary>Tokens spent thinking. Billed against the same budget as the answer, so a
    /// reasoning model can exhaust <c>max_tokens</c> before emitting anything at all.</summary>
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; init; }
}

internal sealed class WireError
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("code")]
    public int Code { get; init; }
}
