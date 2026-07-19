namespace StoryWeaver.Llm;

/// <summary>
/// One message in a conversation. Role is the wire value ("system", "user", "assistant")
/// rather than an enum, because it goes straight onto the request and the set is fixed by
/// the OpenAI-compatible API rather than by us.
/// </summary>
public sealed record LlmMessage(string Role, string Content)
{
    public static LlmMessage System(string content) => new("system", content);

    public static LlmMessage User(string content) => new("user", content);

    public static LlmMessage Assistant(string content) => new("assistant", content);
}
