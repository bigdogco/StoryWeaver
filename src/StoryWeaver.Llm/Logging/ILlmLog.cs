using StoryWeaver.Llm.Configuration;

namespace StoryWeaver.Llm.Logging;

/// <summary>
/// Where prompts, responses, and diagnostics go. An interface so the client is testable and
/// so the eventual UI can surface the same information without going through files.
///
/// This is not incidental plumbing: debugging the extraction pass means reading exactly what
/// was sent and exactly what came back, and reconstructing that after the fact is impossible.
/// </summary>
public interface ILlmLog
{
    void Prompt(LlmRole role, string payload);

    void Response(LlmRole role, string payload);

    void Info(string message);

    void Error(string message, Exception? exception = null);
}

/// <summary>Discards everything. For tests, and for callers that do not want logging.</summary>
public sealed class NullLlmLog : ILlmLog
{
    public static readonly NullLlmLog Instance = new();

    public void Prompt(LlmRole role, string payload) { }

    public void Response(LlmRole role, string payload) { }

    public void Info(string message) { }

    public void Error(string message, Exception? exception = null) { }
}
