namespace StoryWeaver.Llm;

/// <summary>
/// Sends a <see cref="LlmCall"/> and returns the result. The only surface the turn loop
/// needs, and the only one it should use.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Run a call to completion.
    /// </summary>
    /// <param name="onChunk">
    /// Optional. Receives text incrementally as it arrives.
    ///
    /// Streaming is not implemented yet, so today this fires exactly once with the complete
    /// text. It exists now so that implementing real streaming later is a change inside the
    /// client and the renderer, not a change to this interface or to the turn loop — the two
    /// things everything else depends on. Callers that pass null are unaffected either way.
    ///
    /// Deliberately a callback rather than <c>IAsyncEnumerable&lt;string&gt;</c>: the result
    /// carries usage, the serving model, and error information, and splitting those away
    /// from the text stream would complicate every caller to serve a feature we have not
    /// built.
    /// </param>
    Task<LlmResult> CompleteAsync(
        LlmCall call,
        Action<string>? onChunk = null,
        CancellationToken cancellationToken = default);
}
