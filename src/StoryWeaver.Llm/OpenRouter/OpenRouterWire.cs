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

    /// <summary>
    /// The model's thinking, when the provider separates it out.
    ///
    /// Read because some providers put the <i>answer</i> here and leave
    /// <see cref="Content"/> null — observed with MiniMax M3 served by Parasail on vLLM,
    /// which returned perfectly formed delta JSON in this field on every call while
    /// <c>content</c> stayed null. Without the fallback those look identical to empty
    /// responses, and the model scored zero on an eval it was actually passing.
    /// </summary>
    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; init; }
}

internal sealed class WireProvider
{
    [JsonPropertyName("require_parameters")]
    public bool RequireParameters { get; init; }

    /// <summary>
    /// Providers to try, in order. Paired with <see cref="AllowFallbacks"/> = false this pins
    /// the request to one upstream.
    ///
    /// <b>A test instrument, not a runtime dependency.</b> It exists so a sweep can sample
    /// each provider deliberately instead of waiting for price-weighted routing to happen to
    /// land there — you cannot measure providers you are never sent to. Play never sets this;
    /// depending on a single provider at runtime is exactly the fragility we are avoiding.
    /// </summary>
    [JsonPropertyName("order")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Order { get; init; }

    [JsonPropertyName("allow_fallbacks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AllowFallbacks { get; init; }

    /// <summary>
    /// Providers to exclude, keeping every other one.
    ///
    /// The runtime lever, and deliberately weaker than pinning: routing keeps all remaining
    /// providers and their redundancy, and a proxy that does not understand this parameter
    /// degrades to unfiltered routing rather than failing. Intended to be populated from
    /// measurement — a provider that returns schema-valid but semantically wrong deltas cannot
    /// be filtered by <see cref="RequireParameters"/>, because it does support the parameter.
    /// </summary>
    [JsonPropertyName("ignore")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Ignore { get; init; }
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

    /// <summary>
    /// Which upstream provider actually served this request.
    ///
    /// One model id is routed across several providers, and they do not behave identically —
    /// the same scenario has produced correct deltas on one and malformed ones on another
    /// within the same sweep. Without this recorded, a behaviour change looks like the model
    /// drifting, or like whatever we last edited, and both are unfalsifiable.
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    [JsonPropertyName("choices")]
    public List<WireChoice>? Choices { get; init; }

    [JsonPropertyName("usage")]
    public WireUsage? Usage { get; init; }

    [JsonPropertyName("error")]
    public WireError? Error { get; init; }

    /// <summary>
    /// First choice's text, falling back to the reasoning field when content is empty.
    ///
    /// The fallback only applies when there is nothing else — a provider that fills both is
    /// unaffected, so this cannot smuggle thinking into a normal response.
    ///
    /// <b>Except when generation was truncated.</b> Empty content plus reasoning has two very
    /// different causes, and only one of them is safe to recover from:
    ///
    /// <list type="bullet">
    /// <item>the provider <i>misreported</i> — it put the answer in the wrong field, and the
    /// reasoning text is the payload we want;</item>
    /// <item>the model <i>ran out of tokens while thinking</i> — the reasoning text is a
    /// half-finished train of thought and there is no answer at all.</item>
    /// </list>
    ///
    /// <c>finish_reason: "length"</c> separates them, and getting this wrong is not a subtle
    /// failure. Live play produced a narration turn where qwen3.7-plus spent all 1200 of its
    /// tokens reasoning (<c>reasoning_tokens: 1200</c>, <c>content: null</c>) and the fallback
    /// printed 4,682 characters of "Thinking Process: 1. Analyze the Player's Input" into the
    /// story. Extraction was then handed that as the narration and correctly found nothing in
    /// it. A hard failure the turn loop already knows how to report is strictly better than
    /// prose that is not prose.
    /// </summary>
    public string? Content
    {
        get
        {
            if (Choices is not { Count: > 0 } || Choices[0].Message is not { } message)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(message.Content))
            {
                return message.Content;
            }

            return WasTruncated ? null : message.Reasoning;
        }
    }

    /// <summary>True when the payload arrived in the reasoning field. Worth logging: it means
    /// this provider is misreporting, and the next model to "return nothing" may be doing the
    /// same thing.</summary>
    public bool ContentCameFromReasoning =>
        Choices is { Count: > 0 }
        && Choices[0].Message is { } message
        && string.IsNullOrWhiteSpace(message.Content)
        && !string.IsNullOrWhiteSpace(message.Reasoning)
        && !WasTruncated;

    /// <summary>
    /// Generation stopped because it hit the token ceiling, so whatever came back is a
    /// fragment rather than an answer.
    /// </summary>
    public bool WasTruncated =>
        string.Equals(FinishReason, "length", StringComparison.OrdinalIgnoreCase);

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
