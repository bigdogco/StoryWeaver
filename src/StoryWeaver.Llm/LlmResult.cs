namespace StoryWeaver.Llm;

/// <summary>
/// The outcome of a call. Failures are returned rather than thrown: a model refusing,
/// timing out, or emitting garbage is an expected condition in a turn loop, not an
/// exceptional one, and the caller usually wants to degrade rather than unwind.
/// </summary>
public sealed class LlmResult
{
    public required bool IsSuccess { get; init; }

    /// <summary>The model's text. Empty on failure.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Human-readable failure description. Null on success.</summary>
    public string? Error { get; init; }

    /// <summary>Which model actually served the request, as reported by the provider.
    /// Worth logging — with OpenRouter routing this is not always what you asked for.</summary>
    public string? Model { get; init; }

    public LlmUsage? Usage { get; init; }

    /// <summary>HTTP calls made, including retries and repair round-trips. Cost signal.</summary>
    public int Attempts { get; init; }

    public static LlmResult Success(string content, string? model, LlmUsage? usage, int attempts) =>
        new() { IsSuccess = true, Content = content, Model = model, Usage = usage, Attempts = attempts };

    public static LlmResult Failure(string error, int attempts = 0) =>
        new() { IsSuccess = false, Error = error, Attempts = attempts };
}

/// <summary>
/// Token counts for one call. <paramref name="ReasoningTokens"/> is part of
/// <paramref name="CompletionTokens"/>, not additional to it — and on a reasoning model it
/// is usually most of it. Measured at 644 of 736 completion tokens on one extraction sample,
/// which makes it the dominant cost of a turn and worth reporting separately.
/// </summary>
public sealed record LlmUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    int ReasoningTokens = 0);
