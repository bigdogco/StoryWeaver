namespace StoryWeaver.Llm.Configuration;

/// <summary>
/// What a call is *for*, not which model serves it. Code asks for a role; configuration
/// maps the role to a model. Narration and extraction want genuinely different models —
/// creative and expensive vs. cheap, fast, and reliable at structured output — and being
/// able to tune them independently is most of the cost control.
/// </summary>
public enum LlmRole
{
    /// <summary>Creative prose. The player-facing output, and the cost driver.</summary>
    Narration,

    /// <summary>Reads narration, emits structured state deltas. Wants determinism.</summary>
    Extraction,

    /// <summary>Compresses history. Not used in bootstrap.</summary>
    Summarize,

    /// <summary>Generates new world content on demand. Not used in bootstrap.</summary>
    Worldgen,
}

/// <summary>
/// How the model is asked to shape its response.
/// </summary>
public enum LlmResponseFormat
{
    /// <summary>Free-form prose. No constraint.</summary>
    Text,

    /// <summary>Valid JSON, but no schema enforcement. The fallback when a model
    /// does not support schema-constrained decoding.</summary>
    JsonObject,

    /// <summary>Schema-constrained JSON. Preferred for extraction where supported.</summary>
    JsonSchema,
}
