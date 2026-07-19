using StoryWeaver.Llm.Configuration;

namespace StoryWeaver.Llm;

/// <summary>
/// What a caller asks for. Names a <see cref="LlmRole"/> rather than a model — configuration
/// resolves the role to a model, temperature, and response format.
/// </summary>
public sealed class LlmCall
{
    public required LlmRole Role { get; init; }

    public required IReadOnlyList<LlmMessage> Messages { get; init; }

    /// <summary>
    /// Required when the role's configured response format is
    /// <see cref="LlmResponseFormat.JsonSchema"/>; ignored otherwise.
    /// </summary>
    public JsonSchemaSpec? Schema { get; init; }

    /// <summary>
    /// Optional check applied to a successful response's content. Returning false triggers
    /// a repair round-trip: the model is shown its own output and asked to convert it into
    /// the required shape.
    ///
    /// This is the second line of defence behind schema-constrained decoding. Cheap models
    /// emit malformed JSON often enough that re-asking is worth the token cost, and it is
    /// the *primary* mechanism when a model does not support schemas at all.
    /// </summary>
    public Func<string, bool>? Validator { get; init; }

    /// <summary>Overrides the role's configured token budget for this call only.</summary>
    public int? MaxTokens { get; init; }
}

/// <summary>
/// A JSON Schema for schema-constrained output. <paramref name="Schema"/> is raw JSON —
/// the schema object itself, not the wrapper OpenRouter expects around it.
/// </summary>
/// <param name="Name">Schema name sent to the provider. Identifier-ish, no spaces.</param>
/// <param name="Schema">The JSON Schema object, serialized.</param>
/// <param name="Strict">
/// Whether to demand exact conformance. Defaults true; providers that support schemas
/// generally support strict mode, and a loose schema defeats the point.
/// </param>
public sealed record JsonSchemaSpec(string Name, string Schema, bool Strict = true);
